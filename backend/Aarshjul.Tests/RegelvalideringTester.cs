using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Generering;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

public class RegelvalideringTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private IMaltjeneste Mal => new Maltjeneste(_t.Factory);
    private IMalDatautveksling Datautveksling => new MalDatautveksling(_t.Factory, TimeProvider.System);

    public static TheoryData<Regeltype, string?> UgyldigeParametre => new()
    {
        { Regeltype.FastDato, "{}" },
        { Regeltype.FastDato, "{\"dag\":20}" },
        { Regeltype.FastDato, "{\"maaned\":3}" },
        { Regeltype.FastDato, "{\"maaned\":0,\"dag\":20}" },
        { Regeltype.FastDato, "{\"maaned\":13,\"dag\":20}" },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":0}" },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":32}" },
        { Regeltype.FastDato, "{\"maaned\":2,\"dag\":30}" },
        { Regeltype.FastDato, "{\"maaned\":4,\"dag\":31}" },
        { Regeltype.RelativUkedag, "{}" },
        { Regeltype.RelativUkedag, "{\"uke\":2,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":2}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":2,\"ukedag\":null}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":2,\"ukedag\":\"\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":2,\"ukedag\":\"ukjent\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":0,\"uke\":2,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":13,\"uke\":2,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":0,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":6,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":7,\"fra_dag\":0,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":7,\"fra_dag\":32,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":2,\"fra_dag\":30,\"ukedag\":\"man\"}" },
        { Regeltype.RelativTilMilepael, "{}" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\"}" },
        { Regeltype.RelativTilMilepael, "{\"offset_dager\":0}" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":null,\"offset_dager\":0}" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\" \",\"offset_dager\":0}" },
        { Regeltype.FastDato, null },
        { Regeltype.FastDato, " " },
        { Regeltype.FastDato, "{" },
        { Regeltype.FastDato, "null" },
        { Regeltype.FastDato, "[]" },
        { Regeltype.FastDato, "{\"maaned\":null,\"dag\":20}" },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":\"feil type\"}" },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":null}" },
        { Regeltype.RelativUkedag, null },
        { Regeltype.RelativUkedag, " " },
        { Regeltype.RelativUkedag, "{" },
        { Regeltype.RelativUkedag, "null" },
        { Regeltype.RelativUkedag, "[]" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":2,\"ukedag\":1}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"uke\":null,\"ukedag\":\"man\"}" },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"fra_dag\":\"feil type\",\"ukedag\":\"man\"}" },
        { Regeltype.RelativTilMilepael, null },
        { Regeltype.RelativTilMilepael, " " },
        { Regeltype.RelativTilMilepael, "{" },
        { Regeltype.RelativTilMilepael, "null" },
        { Regeltype.RelativTilMilepael, "[]" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":3,\"offset_dager\":0}" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"anker\",\"offset_dager\":null}" },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"anker\",\"offset_dager\":\"feil type\"}" },
        { (Regeltype)99, "{\"maaned\":3,\"dag\":20}" }
    };

    public static TheoryData<Regeltype, string, int, DateOnly> GyldigeParametre => new()
    {
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}", 2026, new(2026, 3, 20) },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":-1}", 2026, new(2025, 3, 20) },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":0}", 2026, new(2026, 3, 20) },
        { Regeltype.FastDato, "{\"maaned\":3,\"dag\":20,\"aar_forskyvning\":1}", 2026, new(2027, 3, 19) },
        { Regeltype.FastDato, "{\"maaned\":2,\"dag\":29}", 2028, new(2028, 2, 29) },
        { Regeltype.RelativUkedag, "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}", 2030, new(2030, 7, 22) },
        { Regeltype.RelativUkedag, "{\"maaned\":7,\"ukedag\":\" Mandag \",\"fra_dag\":20,\"uke\":99}", 2030, new(2030, 7, 22) },
        { Regeltype.RelativUkedag, "{\"maaned\":2,\"ukedag\":\"tir\",\"fra_dag\":29}", 2028, new(2028, 2, 29) },
        { Regeltype.RelativUkedag, "{\"maaned\":2,\"ukedag\":\"man\",\"uke\":5}", 2026, new(2026, 2, 23) },
        { Regeltype.RelativUkedag, "{\"maaned\":2,\"ukedag\":\"man\",\"uke\":5}", 2028, new(2028, 2, 28) },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"ukedag\":\"man\",\"uke\":1,\"aar_forskyvning\":-1}", 2026, new(2025, 3, 3) },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"ukedag\":\"man\",\"uke\":1,\"aar_forskyvning\":0}", 2026, new(2026, 3, 2) },
        { Regeltype.RelativUkedag, "{\"maaned\":3,\"ukedag\":\"man\",\"uke\":1,\"aar_forskyvning\":1}", 2026, new(2027, 3, 1) },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":-1}", 2028, new(2028, 2, 29) },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":0}", 2028, new(2028, 3, 1) },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":1}", 2028, new(2028, 3, 2) },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":-366}", 2028, new(2027, 3, 1) },
        { Regeltype.RelativTilMilepael, "{\"anker_loep\":\"manuelt-anker\",\"offset_dager\":366}", 2028, new(2029, 3, 2) }
    };

    [Theory]
    [MemberData(nameof(GyldigeParametre))]
    public async Task Gyldige_regler_kan_opprettes_oppdateres_og_importeres_med_samme_beregning(
        Regeltype type, string json, int budsjettaar, DateOnly forventet)
    {
        var mal = Mal;
        var inndata = Inndata("gyldig", type, json);
        var id = await mal.OpprettAsync(inndata);
        inndata.Tittel = "Oppdatert";
        await mal.OppdaterAsync(id, inndata);
        var utveksling = Datautveksling;
        var foer = await utveksling.EksporterAsync();

        var resultat = await utveksling.ImporterAsync(foer);

        Assert.Equal(1, resultat.AntallImportert);
        Assert.Empty(resultat.Advarsler);
        var regel = Assert.Single((await utveksling.EksporterAsync()).Regler);
        Assert.Equal(Assert.Single(foer.Regler), regel);
        var beregnet = Assert.Single(Genereringsberegning.Beregn(budsjettaar, [Domeneregel(regel)],
            new Dictionary<string, DateOnly> { ["manuelt-anker"] = new(2028, 3, 1) }));
        Assert.Null(beregnet.Feil);
        Assert.Equal(forventet, beregnet.Dato);
    }

    [Fact]
    public async Task Alle_standardregler_beholder_datoene_etter_manuell_lagring_og_import()
    {
        var mal = Mal;
        foreach (var (loep, tittel, kategori, type, parametre, valgaarssensitiv) in Startdata.Standardregler)
        {
            var inndata = Inndata(loep, type, parametre);
            inndata.Tittel = tittel;
            inndata.Kategori = kategori;
            inndata.Valgaarssensitiv = valgaarssensitiv;
            await mal.OpprettAsync(inndata);
        }
        var utveksling = Datautveksling;
        var foer = await utveksling.EksporterAsync();

        var resultat = await utveksling.ImporterAsync(foer);

        Assert.Equal(7, resultat.AntallImportert);
        Assert.Empty(resultat.Advarsler);
        var etter = await utveksling.EksporterAsync();
        Assert.Equal(foer.Regler, etter.Regler);
        var forventedeDatoer = new Dictionary<string, DateOnly>
        {
            ["marskonferanse"] = new(2027, 3, 1), ["rammefordeling"] = new(2027, 3, 19),
            ["rnb"] = new(2028, 5, 15), ["gulbok"] = new(2027, 9, 15),
            ["nysaldering"] = new(2027, 11, 19), ["statsregnskap"] = new(2029, 2, 15),
            ["rapportering"] = new(2029, 1, 31)
        };
        foreach (var beregnet in Genereringsberegning.Beregn(2028, etter.Regler.Select(Domeneregel).ToList()))
        {
            Assert.Null(beregnet.Feil);
            Assert.Equal(forventedeDatoer[beregnet.Regel.Loep], beregnet.Dato);
        }
    }

    [Fact]
    public async Task Relative_ankerkjeder_bevares_og_skuddag_gir_kontrollert_feil_i_andre_aar()
    {
        var mal = Mal;
        await mal.OpprettAsync(Inndata("anker", Regeltype.FastDato, "{\"maaned\":2,\"dag\":29,\"aar_forskyvning\":-1}"));
        await mal.OpprettAsync(Inndata("samme-dag", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"anker\",\"offset_dager\":0}"));
        await mal.OpprettAsync(Inndata("dagen-foer", Regeltype.RelativTilMilepael, "{\"anker_loep\":\"samme-dag\",\"offset_dager\":-1}"));
        var utveksling = Datautveksling;
        var database = await utveksling.EksporterAsync();
        await utveksling.ImporterAsync(database);
        var regler = (await utveksling.EksporterAsync()).Regler.Select(Domeneregel).ToList();

        var beregnet = Genereringsberegning.Beregn(2029, regler);

        Assert.All(beregnet, r => Assert.Null(r.Feil));
        Assert.Equal(new DateOnly(2028, 2, 29), Assert.Single(beregnet, r => r.Regel.Loep == "samme-dag").Dato);
        Assert.Equal(new DateOnly(2028, 2, 28), Assert.Single(beregnet, r => r.Regel.Loep == "dagen-foer").Dato);
        Assert.All(Genereringsberegning.Beregn(2030, regler), r =>
        {
            Assert.NotNull(r.Feil);
            Assert.Null(r.Dato);
        });
    }

    [Theory]
    [MemberData(nameof(UgyldigeParametre))]
    public async Task Manuell_lagring_avviser_ugyldige_parametre_uten_aa_endre_malen(Regeltype type, string? json)
    {
        var mal = Mal;
        var id = await mal.OpprettAsync(Inndata("opprinnelig", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}"));
        var foer = Assert.Single(await mal.HentAlleAsync());
        var ugyldig = Inndata("endret", type, json);

        await Assert.ThrowsAsync<Valideringsfeil>(() => mal.OpprettAsync(ugyldig));
        Assert.Equal(foer, Assert.Single(await mal.HentAlleAsync()));
        await Assert.ThrowsAsync<Valideringsfeil>(() => mal.OppdaterAsync(id, ugyldig));
        Assert.Equal(foer, Assert.Single(await mal.HentAlleAsync()));
    }

    [Theory]
    [MemberData(nameof(UgyldigeParametre))]
    public async Task Import_advarer_og_utelater_ugyldig_regel_mens_gyldig_regel_erstatter_malen(Regeltype type, string? json)
    {
        await Mal.OpprettAsync(Inndata("gammel", Regeltype.FastDato, "{\"maaned\":1,\"dag\":1}"));
        var utveksling = Datautveksling;
        var gyldig = new MalRegelEksport
        {
            Id = Guid.NewGuid(), Loep = "gyldig", Tittel = "Gyldig regel",
            Regeltype = Regeltype.FastDato, Regelparametre = "{\"maaned\":3,\"dag\":20}"
        };
        var ugyldig = new MalRegelEksport
        {
            Id = Guid.NewGuid(), Loep = "ugyldig", Tittel = "Ugyldig regel",
            Regeltype = type, Regelparametre = json!
        };

        var resultat = await utveksling.ImporterAsync(new MalDatabase { Regler = [ugyldig, gyldig] });

        Assert.Equal(1, resultat.AntallImportert);
        Assert.Equal(1, resultat.AntallErstattet);
        Assert.Contains("Ugyldig regel", Assert.Single(resultat.Advarsler));
        Assert.Equal(gyldig, Assert.Single((await utveksling.EksporterAsync()).Regler));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Import_gir_en_advarsel_per_ugyldig_regel_og_erstatter_alt_med_de_gyldige(bool taMedGyldige)
    {
        await Mal.OpprettAsync(Inndata("gammel", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}"));
        var ugyldige = UgyldigeParametre.Select((data, i) => new MalRegelEksport
        {
            Loep = $"ugyldig-{i}", Regeltype = (Regeltype)data[0], Regelparametre = (string)data[1]!
        }).ToList();
        var gyldige = taMedGyldige ? new List<MalRegelEksport>
        {
            new() { Id = Guid.NewGuid(), Loep = "a", Regeltype = Regeltype.FastDato, Regelparametre = "{\"maaned\":1,\"dag\":1}" },
            new() { Id = Guid.NewGuid(), Loep = "b", Regeltype = Regeltype.RelativUkedag, Regelparametre = "{\"maaned\":7,\"ukedag\":\"man\",\"fra_dag\":20}" }
        } : [];
        var utveksling = Datautveksling;

        var resultat = await utveksling.ImporterAsync(new MalDatabase { Regler = [.. ugyldige, .. gyldige] });

        Assert.Equal(gyldige.Count, resultat.AntallImportert);
        Assert.Equal(1, resultat.AntallErstattet);
        Assert.Equal(ugyldige.Count, resultat.Advarsler.Count);
        Assert.All(ugyldige, regel => Assert.Single(resultat.Advarsler, a => a.StartsWith($"«{regel.Loep}»")));
        Assert.Equal(gyldige, (await utveksling.EksporterAsync()).Regler);
    }

    [Theory]
    [InlineData("versjon")]
    [InlineData("null-liste")]
    [InlineData("null-rad")]
    [InlineData("duplikat-id")]
    public async Task Filfeil_avviser_hele_importen_uten_aa_endre_malen(string feil)
    {
        await Mal.OpprettAsync(Inndata("behold", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}"));
        var utveksling = Datautveksling;
        var foer = await utveksling.EksporterAsync();
        var database = feil switch
        {
            "versjon" => foer with { Versjon = 2 },
            "null-liste" => foer with { Regler = null! },
            "null-rad" => foer with { Regler = [null!] },
            _ => foer with { Regler = [foer.Regler[0], foer.Regler[0]] }
        };

        await Assert.ThrowsAsync<Valideringsfeil>(() => utveksling.ImporterAsync(database));

        Assert.Equal(foer.Regler, (await utveksling.EksporterAsync()).Regler);
        Assert.Equal(1, (await utveksling.ImporterAsync(foer)).AntallImportert);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Import_beholder_eldre_tom_tittel_mens_manuell_lagring_krever_tittel(string? tittel)
    {
        var inndata = Inndata("eldre-mal", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}");
        inndata.Tittel = tittel!;
        await Assert.ThrowsAsync<Valideringsfeil>(() => Mal.OpprettAsync(inndata));
        Assert.Empty(await Mal.HentAlleAsync());

        var mal = Mal;
        var id = await mal.OpprettAsync(Inndata("opprinnelig", Regeltype.FastDato, inndata.Regelparametre));
        var foer = Assert.Single(await mal.HentAlleAsync());
        await Assert.ThrowsAsync<Valideringsfeil>(() => mal.OppdaterAsync(id, inndata));
        Assert.Equal(foer, Assert.Single(await mal.HentAlleAsync()));

        var utveksling = Datautveksling;
        var resultat = await utveksling.ImporterAsync(new MalDatabase
        {
            Regler = [new MalRegelEksport
            {
                Loep = inndata.Loep, Tittel = tittel!, Regeltype = inndata.Regeltype,
                Regelparametre = inndata.Regelparametre
            }]
        });

        Assert.Equal(1, resultat.AntallImportert);
        Assert.Empty(resultat.Advarsler);
        Assert.Equal("", Assert.Single((await utveksling.EksporterAsync()).Regler).Tittel);
    }

    [Fact]
    public async Task Import_av_eldre_json_uten_tittel_beholder_regelen()
    {
        var database = JsonSerializer.Deserialize<MalDatabase>("""
            {"regler":[{"loep":"eldre","regeltype":"FastDato","regelparametre":"{\"maaned\":3,\"dag\":20}"}]}
            """, Aarshjul.Web.Api.FristEndepunkter.DatabaseJson)!;

        var resultat = await Datautveksling.ImporterAsync(database);

        Assert.Equal(1, resultat.AntallImportert);
        Assert.Empty(resultat.Advarsler);
        Assert.Equal("", Assert.Single(await Mal.HentAlleAsync()).Tittel);
    }

    [Fact]
    public async Task Lagringsfeil_etter_sletting_ruller_tilbake_og_neste_import_paa_samme_tjeneste_lykkes()
    {
        await Mal.OpprettAsync(Inndata("gammel", Regeltype.FastDato, "{\"maaned\":3,\"dag\":20}"));
        var utveksling = Datautveksling;
        var foer = (await utveksling.EksporterAsync()).Regler;
        var database = new MalDatabase
        {
            Regler = [new MalRegelEksport
            {
                Loep = "ny", Tittel = "Ny", Regeltype = Regeltype.FastDato,
                Regelparametre = "{\"maaned\":5,\"dag\":2}"
            }]
        };
        await _t.Db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER avvis_malimport AFTER DELETE ON Gjentaksregler
            BEGIN SELECT RAISE(ABORT, 'Simulert feil etter sletting'); END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => utveksling.ImporterAsync(database));

        Assert.Equal(foer, (await utveksling.EksporterAsync()).Regler);
        await _t.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER avvis_malimport;");
        var resultat = await utveksling.ImporterAsync(database);
        Assert.Equal(1, resultat.AntallImportert);
        Assert.Equal(1, resultat.AntallErstattet);
        Assert.Equal("ny", Assert.Single((await utveksling.EksporterAsync()).Regler).Loep);
    }

    private static MalRegelInndata Inndata(string loep, Regeltype type, string? json) => new()
    {
        Loep = loep,
        Tittel = $"Tittel {loep}",
        Kategori = Kategori.Budsjett,
        Regeltype = type,
        Regelparametre = json!
    };

    private static Gjentaksregel Domeneregel(MalRegelEksport regel) => new()
    {
        Id = regel.Id, Loep = regel.Loep, Tittel = regel.Tittel, Kategori = regel.Kategori,
        Regeltype = regel.Regeltype, Regelparametre = regel.Regelparametre,
        Valgaarssensitiv = regel.Valgaarssensitiv
    };

    public void Dispose() => _t.Dispose();
}
