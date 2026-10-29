using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Epost;
using System.Text.Json;

namespace Aarshjul.Infrastructure.Datautveksling;

/// <summary>
/// Eksporterer og importerer alle frister som en JSON-«database» (endring #2). Import erstatter
/// alt: eksisterende frister (og deres synlighetskoblinger) fjernes, og fila blir fasiten.
/// Synlighetskoder som ikke finnes som grupper, hoppes over med en advarsel — da unngår vi å
/// bryte fremmednøkkelen mot <see cref="Synlighetsgruppe"/>. Ingen synlighet slippes til klienten
/// her; dette er en administratoroperasjon i backend.
/// </summary>
public sealed class FristDatautveksling(IDbContextFactory<AppDbContext> dbFactory, TimeProvider klokke) : IFristDatautveksling
{
    public async Task<FristDatabase> EksporterAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var frister = await db.Frister
            .AsNoTracking()
            .Include(f => f.Synlighet)
            .OrderBy(f => f.Budsjettaar)
            .ThenBy(f => f.Sorteringsdag)
            .ToListAsync(ct);

        var eksport = frister.Select(f => new FristEksport
        {
            Id = f.Id,
            Tittel = f.Tittel,
            Dato = f.Dato,
            Datopresisjon = f.Datopresisjon,
            Datokvalifikator = f.Datokvalifikator,
            Klokkeslett = f.Klokkeslett,
            Budsjettaar = f.Budsjettaar,
            Kategori = f.Kategori,
            Loep = f.Loep,
            Kilde = f.Kilde,
            DokumentId = f.DokumentId,
            ManuellKildeUrl = f.ManuellKildeUrl,
            Status = f.Status,
            Opphav = f.Opphav,
            ForeslaattAv = f.ForeslaattAv,
            Notat = f.Notat,
            GjentaRegelId = f.GjentaRegelId,
            SynligFor = f.Synlighet.Select(s => s.GruppeKode).OrderBy(k => k).ToList()
        }).ToList();

        return new FristDatabase
        {
            Versjon = 1,
            EksportertTid = klokke.GetUtcNow(),
            Frister = eksport
        };
    }

    public async Task<FristImportForhaandsvisning> ForhaandsvisAsync(FristDatabase database, CancellationToken ct = default)
    {
        Kontroller(database);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var klargjort = await KlargjoerAsync(db, database, ct);
        return new(await db.Frister.CountAsync(ct), klargjort.Frister, klargjort.Advarsler);
    }

    public async Task<ImportResultat> ImporterAsync(FristDatabase database, CancellationToken ct = default)
    {
        Kontroller(database);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Hele kandidaten og de kjente gruppene kontrolleres før sletting spores.
        var klargjort = await KlargjoerAsync(db, database, ct);
        var kandidater = klargjort.Frister.Select(TilFrist).ToList();

        // Sletting og innlegging må lykkes samlet, også hvis innleggingen feiler.
        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        db.KoordinerEpostlagring();
        try
        {
            // Erstatt-alt: fjern eksisterende frister (synlighet kaskaderes via FristSynlighet-FK).
            var eksisterende = await db.Frister.Include(f => f.Synlighet).ToListAsync(ct);
            var hendelser = await LagHendelserAsync(db, eksisterende, kandidater, ct);
            db.NyFristHendelser.AddRange(hendelser);
            var antallErstattet = eksisterende.Count;
            db.Frister.RemoveRange(eksisterende);

            // Fjern tilbakestående synlighetsrader eksplisitt (robust dersom kaskade er avslått i test).
            db.FristSynlighet.RemoveRange(db.FristSynlighet);
            await db.SaveChangesAsync(ct);

            db.Frister.AddRange(kandidater);

            await db.SaveChangesAsync(ct);
            await transaksjon.CommitAsync(ct);
            return new ImportResultat(kandidater.Count, antallErstattet, klargjort.Advarsler);
        }
        catch
        {
            await transaksjon.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<List<NyFristHendelse>> LagHendelserAsync(AppDbContext db, List<Frist> foer,
        List<Frist> etter, CancellationToken ct)
    {
        var publisertFoer = foer.Where(f => f.Status == FristStatus.Godkjent).ToDictionary(f => f.Id);
        var publisertEtter = etter.Where(f => f.Status == FristStatus.Godkjent).ToDictionary(f => f.Id);
        var ider = publisertEtter.Keys.ToList();
        // Første Ny er livsvarig per identitet. En eksplisitt gjeninnført ID sammenliknes
        // med siste bevarte publiserte tilstand, også for eldre frister uten Ny-historikk.
        var historikk = await db.NyFristHendelser.AsNoTracking().Where(h => ider.Contains(h.FristId))
            .OrderByDescending(h => h.Publiseringssekvens).ToListAsync(ct);
        var resultat = new List<NyFristHendelse>();
        foreach (var f in publisertEtter.Values)
        {
            Fristtilstand? tidligere = publisertFoer.TryGetValue(f.Id, out var gammel) ? Fristtilstand.Fra(gammel) : null;
            var historiske = historikk.Where(h => h.FristId == f.Id).ToList();
            if (tidligere is null)
            {
                var json = historiske.Select(h => h.Type == "Slettet" ? h.FoerJson : h.EtterJson).FirstOrDefault(j => j is not null);
                if (json is not null) tidligere = JsonSerializer.Deserialize<Fristtilstand>(json);
            }
            NyFristHendelse? hendelse = tidligere is not null
                ? Fristendringshendelse.Lag(f, tidligere, false, null, klokke)
                : historiske.Count == 0 ? new()
                {
                    FristId = f.Id, OpprettetUtcTicks = klokke.GetUtcNow().UtcTicks,
                    EtterJson = JsonSerializer.Serialize(Fristtilstand.Fra(f))
                } : null;
            if (hendelse is not null) resultat.Add(hendelse);
        }
        foreach (var f in publisertFoer.Values.Where(f => !publisertEtter.ContainsKey(f.Id)))
        {
            var hendelse = new NyFristHendelse
            {
                FristId = f.Id, Type = "Slettet", OpprettetUtcTicks = klokke.GetUtcNow().UtcTicks,
                FoerJson = JsonSerializer.Serialize(Fristtilstand.Fra(f))
            };
            resultat.Add(hendelse);
            db.Set<FristHendelsessynlighet>().AddRange(f.Synlighet.Select(s => new FristHendelsessynlighet
            { HendelseId = hendelse.Id, GruppeKode = s.GruppeKode }));
        }
        return resultat;
    }

    private static void Kontroller(FristDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        Importvalidering.Kontroller(database.Versjon, database.Frister, f => f.Id);
        if (database.Frister.Any(f => f.SynligFor is null))
            throw new Aarshjul.Application.Valideringsfeil("Alle frister må ha en synlighetsliste. En tom liste er tillatt.");
        foreach (var frist in database.Frister) Kildelenke.ForLagring(frist.ManuellKildeUrl);
    }

    private static async Task<(IReadOnlyList<FristEksport> Frister, IReadOnlyList<string> Advarsler)> KlargjoerAsync(
        AppDbContext db, FristDatabase database, CancellationToken ct)
    {
        var gyldigeKoder = (await db.Synlighetsgrupper.AsNoTracking().Select(g => g.Kode).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var advarsler = new List<string>();
        var frister = new List<FristEksport>();
        foreach (var kilde in database.Frister)
        {
            var koder = new List<string>();
            foreach (var kode in kilde.SynligFor.Distinct(StringComparer.Ordinal))
            {
                if (gyldigeKoder.Contains(kode)) koder.Add(kode);
                else advarsler.Add($"«{kilde.Tittel}»: hoppet over ukjent synlighetsgruppe «{kode}».");
            }
            frister.Add(kilde with { SynligFor = koder.ToArray(), ManuellKildeUrl = Kildelenke.ForLagring(kilde.ManuellKildeUrl) });
        }
        return (frister, advarsler);
    }

    private static Frist TilFrist(FristEksport kilde)
    {
        var frist = new Frist
        {
            Id = kilde.Id == Guid.Empty ? Guid.NewGuid() : kilde.Id,
            Tittel = kilde.Tittel,
            Dato = kilde.Dato,
            Datopresisjon = kilde.Datopresisjon,
            Datokvalifikator = kilde.Datokvalifikator,
            Klokkeslett = kilde.Klokkeslett,
            Budsjettaar = kilde.Budsjettaar,
            Kategori = kilde.Kategori,
            Loep = kilde.Loep,
            Kilde = kilde.Kilde,
            DokumentId = kilde.DokumentId,
            ManuellKildeUrl = kilde.ManuellKildeUrl,
            Status = kilde.Status,
            Opphav = kilde.Opphav,
            ForeslaattAv = kilde.ForeslaattAv,
            Notat = kilde.Notat,
            GjentaRegelId = kilde.GjentaRegelId,
            Synlighet = kilde.SynligFor.Select(k => new FristSynlighet { GruppeKode = k }).ToList()
        };
        frist.OppdaterSorteringsdag();
        return frist;
    }
}
