using System.Text.Json;
using Aarshjul.Application.Faser;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Faser;
using Aarshjul.Infrastructure.Frister;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Hele lagrede innholdet og offentlige projeksjoner før/etter presentasjonsendringer.</summary>
public static class FasedataBevis
{
    // Oppgraderingsprøver må kunne lese/skrive autentiske rader før #140-kolonnen
    // finnes. Dagens modell og lesetjeneste brukes igjen etter migrasjon.
    public static IDbContextFactory<AppDbContext> HistoriskFristFabrikk(IDbContextFactory<AppDbContext> fabrikk)
    {
        using var db = fabrikk.CreateDbContext();
        return new HistoriskAdapter((DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>());
    }
    private sealed class HistoriskAdapter(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    { public AppDbContext CreateDbContext() => new HistoriskKontekst(options); }
    private sealed class HistoriskKontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        { base.OnModelCreating(b); b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl); b.Ignore<Epostadministratorstans>(); }
    }

    public static async Task LeggTilHistorikkAsync(IDbContextFactory<AppDbContext> fabrikk)
    {
        await using var db = HistoriskFristFabrikk(fabrikk).CreateDbContext();
        await Startdata.SeedAsync(db, new());
        var dokument = new BehandletDokument { Id = Guid.NewGuid(), Kilde = "regjeringen", DokumentNokkel = "UX83-kilde", InnholdHash = "syntetisk-hash", Tittel = "Kilde 83", Url = "https://www.regjeringen.no/no/dokumenter/ux83/id1234567/", BehandletStatus = BehandletStatus.FerdigBehandlet };
        db.BehandledeDokumenter.Add(dokument);
        var regel = await db.Gjentaksregler.FirstAsync();
        var datoer = new[] { new DateOnly(2024, 2, 10), new DateOnly(2029, 11, 20), new DateOnly(2029, 11, 20), new DateOnly(2030, 5, 2), new DateOnly(2034, 8, 30), new DateOnly(2035, 8, 30) };
        for (var i = 0; i < datoer.Length; i++)
        {
            var frist = new Frist { Id = Guid.NewGuid(), Tittel = $"UX83 komplett frist {i}", Dato = datoer[i], Budsjettaar = 2030, Kategori = (Kategori)(i % 3), Loep = "fasebevis", Kilde = "R-83/2026", DokumentId = dokument.Id, Notat = $"Bevar hele innholdet {i}", Opphav = Opphav.Robot, ForeslaattAv = "historisk-innsender", GjentaRegelId = regel.Id, Status = i == 4 ? FristStatus.Fullfoert : FristStatus.Godkjent, Datopresisjon = i == 3 ? Datopresisjon.Maaned : Datopresisjon.Dag, Datokvalifikator = i == 3 ? Datokvalifikator.Ultimo : null, Klokkeslett = i == 1 ? new TimeOnly(23, 45) : null };
            frist.Synlighet.Add(new() { GruppeKode = "FA" });
            if (i % 2 == 0) frist.Synlighet.Add(new() { GruppeKode = "POL" });
            db.Frister.Add(frist);
            db.Forslag.Add(new() { Id = Guid.NewGuid(), Tittel = $"UX83 forslag {i}", Dato = datoer[i], Budsjettaar = 2030, Kategori = frist.Kategori, Status = (FristStatus)(i % 5), Opphav = Opphav.Bruker, KildeEllerInnsender = "historisk-innsender", Notat = "Uendret forslag", ForeslaattSynlighet = "[\"FA\",\"POL\"]", ForslagType = i == 0 ? ForslagType.Endring : ForslagType.NyFrist, EndrerFristId = i == 0 ? frist.Id : null, DokumentId = dokument.Id, UttrekksBevis = [new() { Id = Guid.NewGuid(), Felt = "dato", TolketVerdi = datoer[i].ToString("yyyy-MM-dd"), Kildeutdrag = "Historisk kildeutdrag", Konfidens = .83 }] });
        }
        await db.SaveChangesAsync();
    }

    public static async Task<string> HentAsync(IDbContextFactory<AppDbContext> fabrikk)
    {
        await using var db = fabrikk.CreateDbContext();
        // EF-egenskapene inkluderer samtlige lagrede felt, også forslagets lagringsversjon.
        async Task<string[]> Rader<T>() where T : class
        {
            var rader = await db.Set<T>().ToArrayAsync();
            return rader.Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
                    .Where(p => p.Metadata.Name != nameof(Frist.ManuellKildeUrl) || p.CurrentValue is not null)
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue)))
                .Order(StringComparer.Ordinal).ToArray();
        }
        IFristlesing lesing = new FristTjeneste(fabrikk, TimeProvider.System);
        var alle = await lesing.HentSynligeAsync(new Synlighetskontekst(true, []), new() { InkluderHistorikk = true });
        var fa = await lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe("FA"), new() { InkluderHistorikk = true });
        return JsonSerializer.Serialize(new { Frister = await Rader<Frist>(), Synlighet = await Rader<FristSynlighet>(), Forslag = await Rader<Forslag>(), Bevis = await Rader<UttrekksBevis>(), Regler = await Rader<Gjentaksregel>(), Dokumenter = await Rader<BehandletDokument>(), Alle = alle.OrderBy(f => f.Id), Fa = fa.OrderBy(f => f.Id) });
    }
}
