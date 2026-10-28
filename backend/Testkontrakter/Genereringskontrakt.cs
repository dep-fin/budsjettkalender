using System.Data.Common;
using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Godkjenningsko;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Den faktiske genererings- og publiseringsbanen mot SQLite og SQL Server.</summary>
public abstract class Genereringskontrakt
{
    protected abstract Task<Godkjenningsdatabase> OpprettDatabaseAsync();

    [Fact]
    public async Task Proeve_leser_hele_lagrede_malen_med_ankre_uten_noen_lagringsendring()
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        var foer = await SnapshotAsync(fabrikk);
        IGenereringstjeneste tjeneste = Tjeneste(fabrikk);

        var proeve = await tjeneste.ProevAsync(new(2026,
            new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) }));

        Assert.Equal(2026, proeve.Budsjettaar);
        Assert.Equal(4, proeve.Regler.Count);
        Assert.Equal(proeve.Regler.Count, proeve.Beregninger.Count);
        Assert.Equal(4, proeve.Regler.Select(r => r.Id).Distinct().Count());
        var barn = proeve.Regler.ToList().FindIndex(r => r.Loep == "barn");
        Assert.Equal(new DateOnly(2026, 3, 23), proeve.Beregninger[barn].Dato);
        Assert.Equal(Datopresisjon.Dag, proeve.Beregninger[barn].Presisjon);
        Assert.Single(proeve.Beregninger, b => b.ErFeil);
        Assert.Equal(foer, await SnapshotAsync(fabrikk));

        var generert = await tjeneste.GenererAsync(new(2026,
            new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) }));
        var forslag = await tjeneste.HentGenererteAsync(2026);
        Assert.Equal(3, generert.AntallForslag);
        Assert.Single(generert.Feil);
        for (var i = 0; i < proeve.Regler.Count; i++)
        {
            var regel = proeve.Regler[i];
            var beregning = proeve.Beregninger[i];
            if (beregning.ErFeil)
            {
                Assert.Contains($"{regel.Tittel}: {beregning.Feil}", generert.Feil);
                continue;
            }
            var rad = Assert.Single(forslag, f => f.Tittel == regel.Tittel);
            Assert.Equal(regel.Loep, rad.Loep);
            Assert.Equal(regel.Kategori, rad.Kategori);
            Assert.Equal(beregning.Dato, rad.Dato);
            Assert.Equal(beregning.Presisjon, rad.Datopresisjon);
        }
    }

    [Fact]
    public async Task Regenerering_bevarer_hele_andre_data_og_fjoraarets_presisjon_uten_akkumulering()
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        var bevart = await SnapshotAsync(fabrikk, unntaMaalforslag: true);
        IGenereringstjeneste tjeneste = Tjeneste(fabrikk);
        Guid[] gamleId = [];
        string[]? forrigeVerdier = null;
        for (var i = 0; i < 2; i++)
        {
            var resultat = await tjeneste.GenererAsync(new(2026,
                new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) }));
            Assert.Equal(3, resultat.AntallForslag);
            Assert.Single(resultat.Feil);
            var forslag = await tjeneste.HentGenererteAsync(2026);
            Assert.Equal(3, forslag.Count);
            Assert.DoesNotContain(forslag, f => gamleId.Contains(f.ForslagId));
            gamleId = forslag.Select(f => f.ForslagId).ToArray();
            var samme = forslag.Where(f => string.Equals(f.Loep, "samme", StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.Equal(2, samme.Length);
            foreach (var rad in samme)
            {
                Assert.Equal(new DateOnly(2024, 9, 19), rad.FjoraarDato);
                Assert.Equal(Datopresisjon.Maaned, rad.FjoraarDatopresisjon);
                Assert.Equal(Datokvalifikator.Medio, rad.FjoraarDatokvalifikator);
                Assert.Equal(new[] { "FA", "POL" }, rad.ForeslaattSynlighet.Order());
            }
            var barn = Assert.Single(forslag, f => f.Loep == "barn");
            Assert.Equal(new DateOnly(2026, 3, 23), barn.Dato);
            Assert.Null(barn.FjoraarDato);
            Assert.Null(barn.FjoraarDatopresisjon);
            Assert.Null(barn.FjoraarDatokvalifikator);
            Assert.Equal(new[] { "FA", "FIN-FAG" }, barn.ForeslaattSynlighet.Order());
            Assert.Equal(bevart, await SnapshotAsync(fabrikk, unntaMaalforslag: true));
            await using var kontroll = fabrikk.CreateDbContext();
            var lagret = await kontroll.Forslag.Where(f => f.Budsjettaar == 2026 && f.ForslagType == ForslagType.Generert && f.Status == FristStatus.Forslag).ToListAsync();
            Assert.All(lagret, f =>
            {
                Assert.Equal(Opphav.Admin, f.Opphav);
                Assert.Equal("Generert 2026", f.KildeEllerInnsender);
                Assert.Null(f.Datokvalifikator);
                Assert.Null(f.Notat);
                Assert.Null(f.DokumentId);
                Assert.Null(f.EndrerFristId);
                Assert.NotEqual(Guid.Empty, kontroll.Entry(f).Property<Guid>("Versjon").CurrentValue);
            });
            var verdier = lagret.Select(f => JsonSerializer.Serialize(kontroll.Entry(f).Properties
                .Where(p => p.Metadata.Name is not "Id" and not "Versjon").OrderBy(p => p.Metadata.Name)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))).Order(StringComparer.Ordinal).ToArray();
            if (forrigeVerdier is not null) Assert.Equal(forrigeVerdier, verdier);
            forrigeVerdier = verdier;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Helt_ugyldig_eller_tom_mal_erstatter_bare_aapne_genererte_for_maalaar(bool tomMal)
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        IGenereringstjeneste tjeneste = Tjeneste(fabrikk);
        await tjeneste.GenererAsync(new(2026,
            new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) }));
        await GjoerMalenUberegnbarAsync(fabrikk, tomMal);
        var bevart = await SnapshotAsync(fabrikk, unntaMaalforslag: true);

        var resultat = await tjeneste.GenererAsync(new(2026));

        Assert.Equal(0, resultat.AntallForslag);
        Assert.Equal(tomMal ? 0 : 4, resultat.Feil.Count);
        Assert.Empty(await tjeneste.HentGenererteAsync(2026));
        Assert.Equal(new[] { 2027 }, await tjeneste.HentAarMedForslagAsync());
        Assert.Equal(bevart, await SnapshotAsync(fabrikk, unntaMaalforslag: true));
        await using var kontroll = fabrikk.CreateDbContext();
        Assert.Equal(6, await kontroll.Forslag.CountAsync());
        Assert.Single(await kontroll.Frister.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Faktiske_skriv_rulles_tilbake_ved_tom_og_ikke_tom_erstatning_og_samme_tjeneste_kan_proeve_igjen(bool tomErstatning)
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        if (tomErstatning) await GjoerMalenUberegnbarAsync(fabrikk, tomMal: true);
        var foer = await SnapshotAsync(fabrikk);
        var port = new FeilEtterSkriv(tomErstatning);
        await using var oppsett = fabrikk.CreateDbContext();
        var alternativer = new DbContextOptionsBuilder<AppDbContext>(
            (DbContextOptions<AppDbContext>)oppsett.GetService<IDbContextOptions>())
            .AddInterceptors(port).Options;
        IGenereringstjeneste tjeneste = Tjeneste(new Portfabrikk(alternativer));
        var forespoersel = new GenereringsForespoersel(2026,
            new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) });

        var feil = await Assert.ThrowsAsync<DbUpdateException>(() => tjeneste.GenererAsync(forespoersel));

        Assert.Contains("Kontrollert feil etter faktiske genereringsskriv", feil.ToString());
        Assert.Contains("DELETE", port.UtfoerteSkriv);
        if (tomErstatning) Assert.DoesNotContain("INSERT", port.UtfoerteSkriv);
        else Assert.Contains("INSERT", port.UtfoerteSkriv);
        // Ny kontekst sammenlikner samtlige felt, inkludert lagringsversjon og relaterte rader.
        Assert.Equal(foer, await SnapshotAsync(fabrikk));
        port.Aktiv = false;
        var nytt = await tjeneste.GenererAsync(forespoersel);
        Assert.Equal(tomErstatning ? 0 : 3, nytt.AntallForslag);
        Assert.Equal(nytt.AntallForslag, (await tjeneste.HentGenererteAsync(2026)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Faktisk_generert_maanedsfrist_godkjennes_en_gang_med_kategori_og_aktivt_POL_valg(bool medPol)
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        IGenereringstjeneste generering = Tjeneste(fabrikk);
        await generering.GenererAsync(new(2026,
            new Dictionary<string, DateOnly> { ["eksternt"] = new(2026, 3, 20) }));
        var forslag = Assert.Single(await generering.HentGenererteAsync(2026), f => f.Tittel == "Første regel");
        Assert.True(forslag.Tentativ);
        Assert.Equal(Datopresisjon.Maaned, forslag.Datopresisjon);
        Assert.Equal(new[] { "FA", "POL" }, forslag.ForeslaattSynlighet.Order());
        IGodkjenningsko ko = new GodkjenningskoTjeneste(fabrikk);
        var foer = await SnapshotAsync(fabrikk);
        foreach (var ugyldige in new string[][] { [], ["UKJENT"], ["INAKTIV"], ["FA", "POL"] })
        {
            await Assert.ThrowsAsync<Valideringsfeil>(() => ko.GodkjennAsync(new()
            { ForslagId = forslag.ForslagId, Synlighetskoder = ugyldige }));
            Assert.Equal(foer, await SnapshotAsync(fabrikk));
        }
        var justering = await ko.HentForslagForJusteringAsync(forslag.ForslagId);
        Assert.NotNull(justering);
        Assert.Equal(new[] { "FA" }, justering.Felter.Synlighetskoder);
        var valgte = medPol ? new[] { "FA", "POL" } : ["FA"];
        var inndata = new GodkjennInndata { ForslagId = forslag.ForslagId, Synlighetskoder = valgte, PolBekreftet = medPol };

        var fristId = await ko.GodkjennAsync(inndata);

        Assert.NotEqual(forslag.ForslagId, fristId);
        await Assert.ThrowsAsync<Valideringsfeil>(() => ko.GodkjennAsync(inndata));
        IFristlesing lesing = new FristTjeneste(fabrikk, TimeProvider.System);
        var kalender = Assert.Single(await lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe("FA"),
            new() { FraDato = new(2025, 9, 1), TilDato = new(2025, 9, 30), Budsjettaar = [2026], Kategorier = [Kategori.Gulbok], InkluderHistorikk = true }));
        var budsjett = Assert.Single(await lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe("FA"),
            new() { Budsjettaar = [2026], InkluderHistorikk = true }));
        Assert.Equal(fristId, kalender.Id);
        Assert.Equal(fristId, budsjett.Id);
        Assert.Equal(JsonSerializer.Serialize(kalender with { Synlighet = kalender.Synlighet.Order().ToArray() }),
            JsonSerializer.Serialize(budsjett with { Synlighet = budsjett.Synlighet.Order().ToArray() }));
        Assert.Equal("Første regel", kalender.Tittel);
        Assert.Equal(new DateOnly(2025, 9, 15), kalender.Dato);
        Assert.Equal(2026, kalender.Budsjettaar);
        Assert.Equal(Kategori.Gulbok, kalender.Kategori);
        Assert.Equal(Datopresisjon.Maaned, kalender.Datopresisjon);
        Assert.Null(kalender.Datokvalifikator);
        Assert.Equal(valgte, kalender.Synlighet.Order());
        Assert.Null(await lesing.HentEnAsync(Synlighetskontekst.ForGruppe("FIN-FAG"), fristId));
        Assert.Equal(medPol, await lesing.HentEnAsync(Synlighetskontekst.ForGruppe("POL"), fristId) is not null);
        Assert.Equal(2, (await generering.HentGenererteAsync(2026)).Count);
        await using var kontroll = fabrikk.CreateDbContext();
        Assert.Equal(2, await kontroll.Frister.CountAsync());
        Assert.Single(await kontroll.Varsler.ToListAsync());
    }

    private static async Task GjoerMalenUberegnbarAsync(IDbContextFactory<AppDbContext> fabrikk, bool tomMal)
    {
        await using var db = fabrikk.CreateDbContext();
        var regler = await db.Gjentaksregler.ToListAsync();
        if (tomMal) db.Gjentaksregler.RemoveRange(regler);
        else foreach (var regel in regler) regel.Regelparametre = "{";
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Gammel_generert_forslagsidentitet_kan_ikke_publiseres_etter_regenerering()
    {
        await using var database = await OpprettDatabaseAsync();
        await LeggHistorikkAsync(database);
        var fabrikk = database.Fabrikk();
        IGenereringstjeneste tjeneste = Tjeneste(fabrikk);
        var forespoersel = new GenereringsForespoersel(2026);
        await tjeneste.GenererAsync(forespoersel);
        var gammelt = Assert.Single(await tjeneste.HentGenererteAsync(2026), f => f.Tittel == "Første regel");
        await tjeneste.GenererAsync(forespoersel);
        var foer = await SnapshotAsync(fabrikk);
        IGodkjenningsko ko = new GodkjenningskoTjeneste(fabrikk);

        await Assert.ThrowsAsync<Valideringsfeil>(() => ko.GodkjennAsync(new()
        { ForslagId = gammelt.ForslagId, Synlighetskoder = ["FA"] }));

        Assert.Equal(foer, await SnapshotAsync(fabrikk));
        Assert.DoesNotContain(await tjeneste.HentGenererteAsync(2026), f => f.ForslagId == gammelt.ForslagId);
    }

    private sealed class Portfabrikk(DbContextOptions<AppDbContext> alternativer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(alternativer);
    }

    /// <summary>Feiler først etter fullført DELETE og (for ikke-tom erstatning) INSERT.
    /// Rekkefølgen velges av EF og observeres; testen forutsetter ingen bestemt innbyrdes orden.</summary>
    private sealed class FeilEtterSkriv(bool tomErstatning) : DbCommandInterceptor
    {
        public bool Aktiv { get; set; } = true;
        public List<string> UtfoerteSkriv { get; } = [];

        private bool SkalFeile(DbCommand command, bool harSlettetRad)
        {
            if (!Aktiv) return false;
            var sql = command.CommandText.Replace("\"", "").Replace("[", "").Replace("]", "");
            if (sql.Contains("DELETE FROM Forslag", StringComparison.OrdinalIgnoreCase) && harSlettetRad) UtfoerteSkriv.Add("DELETE");
            // En fullført enkel INSERT har skrevet selv når SQL Server bruker SET NOCOUNT ON.
            else if (sql.Contains("INSERT INTO Forslag", StringComparison.OrdinalIgnoreCase)) UtfoerteSkriv.Add("INSERT");
            else return false;
            return UtfoerteSkriv.Contains("DELETE") && (tomErstatning || UtfoerteSkriv.Contains("INSERT"));
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (SkalFeile(command, result.HasRows || result.RecordsAffected > 0))
            {
                await result.DisposeAsync();
                throw new InvalidOperationException("Kontrollert feil etter faktiske genereringsskriv.");
            }
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (SkalFeile(command, result > 0))
                throw new InvalidOperationException("Kontrollert feil etter faktiske genereringsskriv.");
            return ValueTask.FromResult(result);
        }
    }

    private static GenereringsTjeneste Tjeneste(IDbContextFactory<AppDbContext> fabrikk)
        => new(fabrikk, new Synlighetsregel(Options.Create(new SynlighetsregelOpsjoner
        { StandardKoder = ["FA", "FIN-FAG", "POL"] })));

    private static async Task LeggHistorikkAsync(Godkjenningsdatabase database)
    {
        await using var db = database.Fabrikk().CreateDbContext();
        foreach (var kode in new[] { "FA", "FIN-FAG", "POL", "INAKTIV" })
            db.Synlighetsgrupper.Add(new() { Id = Guid.NewGuid(), Kode = kode, Navn = kode, Aktiv = kode != "INAKTIV" });
        var regel = new Gjentaksregel
        {
            Id = Guid.NewGuid(), Loep = "samme", Tittel = "Første regel", Kategori = Kategori.Gulbok,
            Regeltype = Regeltype.FastDato, Regelparametre = """{"maaned":9,"dag":15,"aar_forskyvning":-1}""", Valgaarssensitiv = true
        };
        db.Gjentaksregler.AddRange(regel,
            new() { Id = Guid.NewGuid(), Loep = "SAMME", Tittel = "Andre regel", Kategori = Kategori.Regnskap, Regeltype = Regeltype.FastDato, Regelparametre = """{"maaned":4,"dag":20}""" },
            new() { Id = Guid.NewGuid(), Loep = "barn", Tittel = "Manuelt barn", Kategori = Kategori.Budsjett, Regeltype = Regeltype.RelativTilMilepael, Regelparametre = """{"anker_loep":"eksternt","offset_dager":3}""" },
            new() { Id = Guid.NewGuid(), Loep = "feil", Tittel = "Ugyldig eldre regel", Kategori = Kategori.Budsjett, Regeltype = Regeltype.FastDato, Regelparametre = "{" });
        var frist = new Frist
        {
            Id = Guid.NewGuid(), Tittel = "Godkjent fjorår", Dato = new(2024, 9, 19),
            Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio,
            Budsjettaar = 2025, Kategori = Kategori.Gulbok, Loep = "SaMmE", Notat = "Bevar fristinnhold",
            Status = FristStatus.Godkjent, Kilde = "manuell", GjentaRegelId = regel.Id,
            Synlighet = [new() { GruppeKode = "FA" }, new() { GruppeKode = "POL" }]
        };
        frist.OppdaterSorteringsdag();
        db.Frister.Add(frist);
        var kombinasjoner = new[]
        {
            (2026, ForslagType.Generert, FristStatus.Forslag, Opphav.Admin),
            (2026, ForslagType.Generert, FristStatus.Forslag, Opphav.Admin),
            (2027, ForslagType.Generert, FristStatus.Forslag, Opphav.Admin),
            (2026, ForslagType.Generert, FristStatus.Godkjent, Opphav.Admin),
            (2026, ForslagType.Generert, FristStatus.Avvist, Opphav.Admin),
            (2026, ForslagType.NyFrist, FristStatus.Forslag, Opphav.Bruker),
            (2026, ForslagType.NyFrist, FristStatus.Forslag, Opphav.Robot),
            (2026, ForslagType.Endring, FristStatus.Forslag, Opphav.Bruker)
        };
        for (var i = 0; i < kombinasjoner.Length; i++)
        {
            var (aar, type, status, opphav) = kombinasjoner[i];
            db.Forslag.Add(new()
            {
                Id = Guid.NewGuid(), Tittel = $"Historisk forslag {i}", Dato = new(2025, 10, 17),
                Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Ultimo,
                Budsjettaar = aar, Kategori = Kategori.Regnskap, Loep = "historikk", Notat = $"Hele innholdet {i}",
                ForslagType = type, Status = status, Opphav = opphav, KildeEllerInnsender = "syntetisk-innsender",
                ForeslaattSynlighet = "[\"FA\",\"POL\"]", EndrerFristId = type == ForslagType.Endring ? frist.Id : null,
                UttrekksBevis = [new() { Id = Guid.NewGuid(), Felt = "dato", TolketVerdi = "2025-10", Kildeutdrag = "Oktober", Konfidens = .86 }]
            });
        }
        db.Varsler.Add(new() { Id = Guid.NewGuid(), BrukerId = "syntetisk-innsender", Tekst = "Historisk varsel", Begrunnelse = "Bevares", Lest = true, Opprettet = new DateTimeOffset(2025, 1, 2, 12, 0, 0, TimeSpan.Zero) });
        await db.SaveChangesAsync();
    }

    private static async Task<string> SnapshotAsync(IDbContextFactory<AppDbContext> fabrikk, bool unntaMaalforslag = false)
    {
        await using var db = fabrikk.CreateDbContext();
        async Task<string[]> Rader<T>(IQueryable<T>? utvalg = null) where T : class
        {
            var rader = await (utvalg ?? db.Set<T>()).ToListAsync();
            return rader.Select(r => JsonSerializer.Serialize(db.Entry(r).Properties.OrderBy(p => p.Metadata.Name)
                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue))).Order(StringComparer.Ordinal).ToArray();
        }
        var forslag = db.Forslag.Where(f => !unntaMaalforslag || f.Budsjettaar != 2026 || f.ForslagType != ForslagType.Generert || f.Status != FristStatus.Forslag);
        var bevis = db.Set<UttrekksBevis>().Where(b => forslag.Any(f => f.Id == b.ForslagId));
        return JsonSerializer.Serialize(new
        {
            Frister = await Rader<Frist>(), Synlighet = await Rader<FristSynlighet>(),
            Forslag = await Rader(forslag), Bevis = await Rader(bevis), Regler = await Rader<Gjentaksregel>(),
            Grupper = await Rader<Synlighetsgruppe>(), Varsler = await Rader<Varsel>()
        });
    }
}
