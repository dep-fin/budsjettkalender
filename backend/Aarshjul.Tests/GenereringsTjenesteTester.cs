using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Godkjenningsko;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Genereringstjenesten (Fase 3 Steg C/D/E) mot SQLite: persistering, synlighet-videreføring, adskilt fra køen.</summary>
public class GenereringsTjenesteTester : IDisposable
{
    private readonly Testdatabase _t = new();

    public GenereringsTjenesteTester()
    {
        foreach (var (kode, navn) in Aarshjul.Infrastructure.Startdata.Standardgrupper)
        {
            _t.Db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = kode, Navn = navn, ErStandard = true });
        }
        _t.Db.SaveChanges();
    }

    private GenereringsTjeneste Tjeneste(params string[] standardKoder)
    {
        var opsjoner = Options.Create(new SynlighetsregelOpsjoner
        {
            StandardKoder = standardKoder.Length > 0 ? standardKoder.ToList() : ["FA", "FIN-FAG"]
        });
        return new GenereringsTjeneste(_t.Factory, new Synlighetsregel(opsjoner));
    }

    private async Task LeggRegel(string loep, string parametre, bool valgaarssensitiv = false,
        Regeltype type = Regeltype.FastDato)
    {
        _t.Db.Gjentaksregler.Add(new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = loep,
            Tittel = $"Tittel {loep}",
            Kategori = Kategori.Budsjett,
            Regeltype = type,
            Regelparametre = parametre,
            Valgaarssensitiv = valgaarssensitiv
        });
        await _t.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Datooverloep_rapporteres_per_regel_og_gyldig_delmengde_bevares_ved_regenerering()
    {
        await LeggRegel("gyldig", """{"maaned":3,"dag":20}""");
        await LeggRegel("siste", """{"anker_loep":"ledd","offset_dager":1}""", type: Regeltype.RelativTilMilepael);
        await LeggRegel("ledd", """{"anker_loep":"overloep","offset_dager":-1}""", type: Regeltype.RelativTilMilepael);
        await LeggRegel("overloep", """{"anker_loep":"manuelt","offset_dager":2147483647}""", type: Regeltype.RelativTilMilepael);
        await LeggRegel("fast-aar", """{"maaned":1,"dag":1,"aar_forskyvning":2147483647}""");
        await LeggRegel("ukedag-aar", """{"maaned":1,"uke":1,"ukedag":"man","aar_forskyvning":-2147483648}""", type: Regeltype.RelativUkedag);
        await LeggRegel("etter-maks", """{"maaned":12,"fra_dag":31,"ukedag":"man","aar_forskyvning":7973}""", type: Regeltype.RelativUkedag);
        await LeggRegel("stor-gyldig", """{"anker_loep":"manuelt","offset_dager":3652058}""", type: Regeltype.RelativTilMilepael);
        await LeggRegel("femte-mandag", """{"maaned":12,"uke":5,"ukedag":"man","aar_forskyvning":7973}""", type: Regeltype.RelativUkedag);
        IGenereringstjeneste tjeneste = Tjeneste();
        var forespoersel = new GenereringsForespoersel(2026,
            new Dictionary<string, DateOnly> { ["manuelt"] = DateOnly.MinValue });

        for (var kjoring = 0; kjoring < 2; kjoring++)
        {
            var resultat = await tjeneste.GenererAsync(forespoersel);

            Assert.Equal(3, resultat.AntallForslag);
            Assert.Equal(6, resultat.Feil.Count);
            foreach (var loep in new[] { "siste", "ledd", "overloep", "fast-aar", "ukedag-aar", "etter-maks" })
            {
                Assert.Single(resultat.Feil, feil => feil.StartsWith($"Tittel {loep}:"));
            }
            var forslag = await tjeneste.HentGenererteAsync(2026);
            Assert.Equal(3, forslag.Count);
            Assert.Equal(new DateOnly(2026, 3, 20), Assert.Single(forslag, f => f.Loep == "gyldig").Dato);
            Assert.Equal(DateOnly.MaxValue, Assert.Single(forslag, f => f.Loep == "stor-gyldig").Dato);
            Assert.Equal(new DateOnly(9999, 12, 27), Assert.Single(forslag, f => f.Loep == "femte-mandag").Dato);
        }
    }

    [Fact]
    public async Task Helt_mislykket_beregning_erstatter_aarets_aapne_genererte_forslag_med_tom_delmengde()
    {
        await LeggRegel("overloep", """{"anker_loep":"manuelt","offset_dager":2147483647}""", type: Regeltype.RelativTilMilepael);
        IGenereringstjeneste tjeneste = Tjeneste();
        var manuelle = new Dictionary<string, DateOnly> { ["overloep"] = new(2026, 3, 20) };
        await tjeneste.GenererAsync(new GenereringsForespoersel(2026, manuelle));
        await tjeneste.GenererAsync(new GenereringsForespoersel(2027, manuelle));
        Assert.Single(await tjeneste.HentGenererteAsync(2026));

        var resultat = await tjeneste.GenererAsync(new GenereringsForespoersel(2026,
            new Dictionary<string, DateOnly> { ["manuelt"] = DateOnly.MinValue }));

        Assert.Equal(0, resultat.AntallForslag);
        Assert.Single(resultat.Feil);
        Assert.Empty(await tjeneste.HentGenererteAsync(2026));
        Assert.Single(await tjeneste.HentGenererteAsync(2027));
        Assert.Equal([2027], await tjeneste.HentAarMedForslagAsync());
    }

    [Fact]
    public async Task Lagringsfeil_ruller_tilbake_hele_regenereringen_og_neste_kall_lykkes()
    {
        await LeggRegel("gyldig", """{"maaned":3,"dag":20}""");
        IGenereringstjeneste tjeneste = Tjeneste();
        var forespoersel = new GenereringsForespoersel(2026,
            new Dictionary<string, DateOnly> { ["manuelt"] = DateOnly.MinValue });
        await tjeneste.GenererAsync(forespoersel);
        var foer = Assert.Single(await tjeneste.HentGenererteAsync(2026));
        await LeggRegel("overloep", """{"anker_loep":"manuelt","offset_dager":2147483647}""", type: Regeltype.RelativTilMilepael);
        await _t.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER avvis_generering AFTER INSERT ON Forslag
            BEGIN SELECT RAISE(ABORT, 'Simulert lagringsfeil'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => tjeneste.GenererAsync(forespoersel));

        var bevart = Assert.Single(await tjeneste.HentGenererteAsync(2026));
        Assert.Equal(foer.ForslagId, bevart.ForslagId);
        Assert.Equal(foer.Dato, bevart.Dato);
        await _t.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_generering;");
        var resultat = await tjeneste.GenererAsync(forespoersel);
        Assert.Equal(1, resultat.AntallForslag);
        Assert.Single(resultat.Feil);
        var nytt = Assert.Single(await tjeneste.HentGenererteAsync(2026));
        Assert.Equal("gyldig", nytt.Loep);
        Assert.Equal(new DateOnly(2026, 3, 20), nytt.Dato);
    }

    private async Task LeggGodkjentFjoraarsfrist(string loep, int budsjettaar, DateOnly dato, params string[] grupper)
    {
        var frist = new Frist
        {
            Id = Guid.NewGuid(),
            Tittel = $"Fjor {loep}",
            Dato = dato,
            Budsjettaar = budsjettaar,
            Kategori = Kategori.Budsjett,
            Loep = loep,
            Status = FristStatus.Godkjent
        };
        foreach (var g in grupper)
        {
            frist.Synlighet.Add(new FristSynlighet { GruppeKode = g });
        }
        _t.Db.Frister.Add(frist);
        await _t.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task Eldre_lagrede_regler_med_ugyldig_json_eller_nullukedag_rapporteres_uten_aa_stanse_genereringen()
    {
        await LeggRegel("gyldig", "{\"maaned\":3,\"dag\":20}");
        _t.Db.Gjentaksregler.AddRange(
            new Gjentaksregel { Loep = "jsonfeil", Regeltype = Regeltype.FastDato, Regelparametre = "{" },
            new Gjentaksregel { Loep = "nullukedag", Regeltype = Regeltype.RelativUkedag, Regelparametre = "{\"maaned\":3,\"uke\":2,\"ukedag\":null}" },
            new Gjentaksregel { Loep = "mangler-offset", Regeltype = Regeltype.RelativTilMilepael, Regelparametre = "{\"anker_loep\":\"gyldig\"}" });
        await _t.Db.SaveChangesAsync();
        IGenereringstjeneste tjeneste = Tjeneste();

        var resultat = await tjeneste.GenererAsync(new GenereringsForespoersel(2026));

        Assert.Equal(1, resultat.AntallForslag);
        Assert.Equal(3, resultat.Feil.Count);
        var forslag = Assert.Single(await tjeneste.HentGenererteAsync(2026));
        Assert.Equal("gyldig", forslag.Loep);
        Assert.Equal(new DateOnly(2026, 3, 20), forslag.Dato);
    }

    [Fact]
    public async Task Generering_lager_forslag_av_type_generert()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        var resultat = await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(1, resultat.AntallForslag);
        var forslag = await _t.Db.Forslag.SingleAsync();
        Assert.Equal(ForslagType.Generert, forslag.ForslagType);
        Assert.Equal(FristStatus.Forslag, forslag.Status);
        Assert.Equal(2030, forslag.Budsjettaar);
    }

    [Fact]
    public async Task Synlighet_viderefoeres_fra_godkjent_fjoraarsfrist_inkludert_pol_som_forslag()
    {
        await LeggRegel("gulbok", "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}");
        await LeggGodkjentFjoraarsfrist("gulbok", 2029, new DateOnly(2029, 9, 15), "FA", "POL");

        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));
        Assert.Contains("FA", dto.ForeslaattSynlighet);
        // POL bæres med som forslag (krever aktiv bekreftelse ved godkjenning, settes aldri stilltiende).
        Assert.Contains("POL", dto.ForeslaattSynlighet);
        Assert.Equal(new DateOnly(2029, 9, 15), dto.FjoraarDato);
    }

    [Fact]
    public async Task Uten_fjoraarsfrist_brukes_synlighetsregelens_default_uten_pol()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");

        await Tjeneste("FA", "FIN-FAG", "POL").GenererAsync(new GenereringsForespoersel(2030));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));
        Assert.Equal(["FA", "FIN-FAG"], dto.ForeslaattSynlighet.OrderBy(k => k).ToArray());
        Assert.DoesNotContain("POL", dto.ForeslaattSynlighet);
    }

    [Fact]
    public async Task Regenerering_gir_ikke_dubletter()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        var t = Tjeneste();

        await t.GenererAsync(new GenereringsForespoersel(2030));
        await t.GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(1, await _t.Db.Forslag.CountAsync(f => f.Budsjettaar == 2030));
    }

    [Fact]
    public async Task Genererte_forslag_vises_ikke_i_den_loepende_koeen()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        var ko = await new GodkjenningskoTjeneste(_t.Factory).HentKoAsync();

        Assert.Empty(ko);
    }

    [Fact]
    public async Task Valgaarssensitiv_frist_i_stortingsvalgaar_blir_tentativ_paa_flaten()
    {
        // Budsjettår 2025: gulbok faller samme år (forskyvning 0) = stortingsvalgår.
        await LeggRegel("gulbok", "{\"maaned\":9,\"dag\":15,\"aar_forskyvning\":0}", valgaarssensitiv: true);

        await Tjeneste().GenererAsync(new GenereringsForespoersel(2025));

        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2025));
        Assert.True(dto.Tentativ);
        Assert.True(dto.Valgaarsflagg);
        Assert.Equal(Datopresisjon.Maaned, dto.Datopresisjon);
    }

    [Fact]
    public async Task Uopploeselig_anker_rapporteres_som_feil_ikke_som_forslag()
    {
        _t.Db.Gjentaksregler.Add(new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = "rammenotat",
            Tittel = "Rammenotat",
            Kategori = Kategori.Budsjett,
            Regeltype = Regeltype.RelativTilMilepael,
            Regelparametre = "{\"anker_loep\":\"finnesikke\",\"offset_dager\":-7}"
        });
        await _t.Db.SaveChangesAsync();

        var resultat = await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));

        Assert.Equal(0, resultat.AntallForslag);
        Assert.NotEmpty(resultat.Feil);
        Assert.Empty(await _t.Db.Forslag.ToListAsync());
    }

    [Fact]
    public async Task Godkjenning_av_generert_forslag_publiserer_frist_med_synlighet()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));
        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));

        var ko = new GodkjenningskoTjeneste(_t.Factory);
        var fristId = await ko.GodkjennAsync(new GodkjennInndata
        {
            ForslagId = dto.ForslagId,
            Synlighetskoder = ["FA"],
            PolBekreftet = false
        });

        var frist = await _t.Db.Frister.Include(f => f.Synlighet).SingleAsync(f => f.Id == fristId);
        Assert.Equal(FristStatus.Godkjent, frist.Status);
        Assert.Equal("marskonferanse", frist.Loep);
        Assert.Contains(frist.Synlighet, s => s.GruppeKode == "FA");
    }

    [Fact]
    public async Task Godkjenning_av_generert_forslag_med_pol_krever_aktiv_bekreftelse()
    {
        await LeggRegel("marskonferanse", "{\"maaned\":3,\"dag\":2,\"aar_forskyvning\":-1}");
        await Tjeneste().GenererAsync(new GenereringsForespoersel(2030));
        var dto = Assert.Single(await Tjeneste().HentGenererteAsync(2030));

        var ko = new GodkjenningskoTjeneste(_t.Factory);
        await Assert.ThrowsAsync<Aarshjul.Application.Valideringsfeil>(() => ko.GodkjennAsync(new GodkjennInndata
        {
            ForslagId = dto.ForslagId,
            Synlighetskoder = ["FA", "POL"],
            PolBekreftet = false
        }));
    }

    public void Dispose() => _t.Dispose();
}
