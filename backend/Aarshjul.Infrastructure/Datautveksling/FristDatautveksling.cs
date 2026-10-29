using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Aarshjul.Infrastructure.Frister;

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
        try
        {
            // Erstatt-alt: fjern eksisterende frister (synlighet kaskaderes via FristSynlighet-FK).
            var eksisterende = await db.Frister.Include(f => f.Synlighet).ToListAsync(ct);
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
