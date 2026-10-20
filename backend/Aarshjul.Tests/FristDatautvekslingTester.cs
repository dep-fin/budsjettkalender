using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Eksport/import av frister som JSON-«database» (endring #2): round-trip bevarer felter og
/// synlighet, import erstatter alt, og ukjente synlighetskoder hoppes over uten å bryte FK.
/// </summary>
public class FristDatautvekslingTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private static readonly DateOnly Idag = new(2026, 1, 1);

    public FristDatautvekslingTester() => SeedAsync().GetAwaiter().GetResult();

    private async Task SeedAsync()
    {
        foreach (var (kode, navn) in Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe
            {
                Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true
            });
        }
        await _t.Db.SaveChangesAsync();
    }

    private FristDatautveksling Tjeneste() => new(_t.Db, new FastKlokke(Idag));

    private void LeggTilFrist(string tittel, string[] grupper, int budsjettaar = 2027)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = tittel,
            Dato = new DateOnly(2026, 6, 1),
            Budsjettaar = budsjettaar,
            Kategori = Kategori.Budsjett,
            Notat = "beskrivende tekst",
            Status = FristStatus.Godkjent
        };
        foreach (var g in grupper)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        }
        _t.Db.Frister.Add(frist);
    }

    [Fact]
    public async Task Eksport_tar_med_felter_og_synlighet()
    {
        LeggTilFrist("Kun FA", ["FA"]);
        LeggTilFrist("FA og FAG", ["FA", "FAG"]);
        await _t.Db.SaveChangesAsync();

        var db = await Tjeneste().EksporterAsync();

        Assert.Equal(2, db.Frister.Count);
        var faFag = Assert.Single(db.Frister, f => f.Tittel == "FA og FAG");
        Assert.Equal(["FA", "FAG"], faFag.SynligFor);
        Assert.Equal("beskrivende tekst", faFag.Notat);
        Assert.Equal(2027, faFag.Budsjettaar);
    }

    [Fact]
    public async Task Import_erstatter_alt_og_bevarer_synlighet()
    {
        LeggTilFrist("Gammel frist", ["FA"]);
        await _t.Db.SaveChangesAsync();

        var database = new FristDatabase
        {
            EksportertTid = new FastKlokke(Idag).GetUtcNow(),
            Frister =
            [
                new FristEksport
                {
                    Id = Guid.NewGuid(), Tittel = "Ny frist", Dato = new DateOnly(2028, 3, 2),
                    Budsjettaar = 2028, Kategori = Kategori.Gulbok, SynligFor = ["FA", "POL"]
                }
            ]
        };

        var r = await Tjeneste().ImporterAsync(database);

        Assert.Equal(1, r.AntallImportert);
        Assert.Equal(1, r.AntallErstattet);

        var etter = await Tjeneste().EksporterAsync();
        var frist = Assert.Single(etter.Frister);
        Assert.Equal("Ny frist", frist.Tittel);
        Assert.Equal(["FA", "POL"], frist.SynligFor);
        // Sorteringsdag beregnes på nytt ved import (ingen tabellrester fra den gamle fristen).
    }

    [Fact]
    public async Task Import_hopper_over_ukjent_gruppekode_med_advarsel()
    {
        var database = new FristDatabase
        {
            Frister =
            [
                new FristEksport
                {
                    Id = Guid.NewGuid(), Tittel = "Med ukjent kode", Dato = new DateOnly(2028, 3, 2),
                    Budsjettaar = 2028, Kategori = Kategori.Budsjett, SynligFor = ["FA", "FINNESIKKE"]
                }
            ]
        };

        var r = await Tjeneste().ImporterAsync(database);

        Assert.Equal(1, r.AntallImportert);
        Assert.Single(r.Advarsler);

        var frist = Assert.Single((await Tjeneste().EksporterAsync()).Frister);
        Assert.Equal(["FA"], frist.SynligFor); // ukjent kode utelatt, ingen FK-feil
    }

    [Fact]
    public async Task Eksport_import_bevarer_klokkeslett()
    {
        LeggTilFrist("Tidfestet frist", ["FA"]);
        _t.Db.ChangeTracker.Entries<Frist>().Single().Entity.Klokkeslett = new TimeOnly(14, 30);
        await _t.Db.SaveChangesAsync();

        var database = await Tjeneste().EksporterAsync();
        await Tjeneste().ImporterAsync(database);

        var lesing = new Aarshjul.Infrastructure.Frister.FristskrivingTjeneste(_t.Db);
        var frist = await lesing.HentForRedigeringAsync(database.Frister.Single().Id);
        Assert.Equal(new TimeOnly(14, 30), frist!.Klokkeslett);
    }

    [Fact]
    public async Task Feil_under_lagring_bevarer_gamle_frister_og_synlighet()
    {
        LeggTilFrist("Behold meg", ["FA", "POL"]);
        await _t.Db.SaveChangesAsync();
        var tjeneste = Tjeneste();
        var foer = await tjeneste.EksporterAsync();
        await _t.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER avvis_import BEFORE INSERT ON Frister
            BEGIN SELECT RAISE(ABORT, 'Simulert lagringsfeil'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => tjeneste.ImporterAsync(foer));

        var etter = Assert.Single((await tjeneste.EksporterAsync()).Frister);
        Assert.Equal(foer.Frister.Single().Id, etter.Id);
        Assert.Equal(["FA", "POL"], etter.SynligFor);
        await _t.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_import;");
        var resultat = await tjeneste.ImporterAsync(foer);
        Assert.Equal(1, resultat.AntallImportert);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Ukjent_filversjon_avvises_uten_aa_slette_frister(int versjon)
    {
        LeggTilFrist("Behold meg", ["FA"]);
        await _t.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<Aarshjul.Application.Valideringsfeil>(() =>
            Tjeneste().ImporterAsync(new FristDatabase { Versjon = versjon }));
        Assert.Single((await Tjeneste().EksporterAsync()).Frister);
    }

    [Fact]
    public void Json_uten_fristliste_er_ikke_en_tom_database()
        => Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<FristDatabase>("{}", Aarshjul.Web.Api.FristEndepunkter.DatabaseJson));

    public void Dispose() => _t.Dispose();
}
