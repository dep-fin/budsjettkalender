using Aarshjul.Application;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.Tests;

/// <summary>Internkalenderens generelle regler og generering av konkret plan (trinn 2).</summary>
public class InternkalenderGenereringTester : IDisposable
{
    private readonly Testdatabase _t = new();
    private readonly InternkalenderTjeneste _kal;
    private readonly GjoeremaalRegelTjeneste _regler;

    public InternkalenderGenereringTester()
    {
        _kal = new InternkalenderTjeneste(_t.Factory, new FastKlokke(new DateOnly(2026, 8, 3)));
        _regler = new GjoeremaalRegelTjeneste(_t.Factory);
    }

    [Fact]
    public async Task Regel_krever_minst_en_rundetype_og_avviser_Ovrig()
    {
        var utenType = await Assert.ThrowsAsync<Valideringsfeil>(
            () => _regler.OpprettAsync(new RegelInndata { Tittel = "X" }));
        Assert.Contains("minst én rundetype", utenType.Message);

        var medOvrig = await Assert.ThrowsAsync<Valideringsfeil>(
            () => _regler.OpprettAsync(new RegelInndata { Tittel = "X", Rundetyper = [Rundetype.Ovrig] }));
        Assert.Contains("genererbare", medOvrig.Message);
    }

    [Fact]
    public async Task Regel_roundtrip_bevarer_rundetyper_og_ansvarlige()
    {
        var id = await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = "Sammenstille",
            Rundetyper = [Rundetype.Marsrunden, Rundetype.Augustrunden],
            Tidfestingstype = Tidfestingstype.Rundeposisjon,
            Rundeposisjon = Rundeposisjon.Tidlig,
            Ansvarlige = [new AnsvarligDto("demo-admin", "Dag"), new AnsvarligDto(null, "Ekstern")]
        });
        var lest = await _regler.HentForRedigeringAsync(id);
        Assert.Equal(2, lest!.Rundetyper.Count);
        Assert.Equal(2, lest.Ansvarlige.Count);
    }

    [Fact]
    public async Task Generering_lager_gjoeremaal_fra_reglene_med_opphav_og_ansvarlige()
    {
        await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = "Forberede notat",
            Rundetyper = [Rundetype.Augustrunden],
            Tidfestingstype = Tidfestingstype.Rundeposisjon,
            Rundeposisjon = Rundeposisjon.Start,
            Ansvarlige = [new AnsvarligDto("demo-admin", "Dag")]
        });

        var runde = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var g = (await _kal.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal("Forberede notat", g.Tittel);
        Assert.Equal(GjoeremaalOpphav.Generert, g.Opphav);
        // Augustrunden 2027 = jul–okt 2026; Start = 1. juli 2026.
        Assert.Equal(new DateOnly(2026, 7, 1), g.Sorteringsdag);
        Assert.Single(g.Ansvarlige);
    }

    [Fact]
    public async Task KonkretDato_regel_bruker_rundens_aarsforskyvning()
    {
        // Marsrunden 2027 = arbeid i 2026 (t-1). En fast dag «20. mars» skal lande i 2026.
        await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = "Rammefordelingsnotat",
            Rundetyper = [Rundetype.Marsrunden],
            Tidfestingstype = Tidfestingstype.KonkretDato,
            Maaned = 3,
            Dag = 20
        });
        var runde = await _kal.GenererRundeAsync(Rundetype.Marsrunden, 2027, "Dag");
        var g = (await _kal.HentGjoeremaalAsync(runde, null)).Single();
        Assert.Equal(2026, g.Sorteringsdag!.Value.Year);
        Assert.Equal(3, g.Sorteringsdag.Value.Month);
    }

    [Fact]
    public async Task Hver_runde_regel_genereres_inn_i_hver_valgt_runde()
    {
        await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = "Fast oppgave i alle runder",
            Rundetyper = [.. Runder.Genererbare],
            Tidfestingstype = Tidfestingstype.Rundeposisjon,
            Rundeposisjon = Rundeposisjon.Midt
        });

        var august = await _kal.GenererRundeAsync(Rundetype.Augustrunden, 2027, "Dag");
        var rnb = await _kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Dag");
        Assert.Single(await _kal.HentGjoeremaalAsync(august, null));
        Assert.Single(await _kal.HentGjoeremaalAsync(rnb, null));
    }

    [Fact]
    public async Task Generering_i_eksisterende_runde_feiler()
    {
        await _kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Dag");
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(
            () => _kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Dag"));
        Assert.Contains("synkronisering", feil.Message);
    }

    [Fact]
    public async Task Anker_regel_oppløses_ved_generering()
    {
        _t.Db.Frister.Add(new Frist
        {
            Tittel = "Rammefordeling",
            Dato = new DateOnly(2027, 3, 20),
            Budsjettaar = 2027,
            Loep = "rammefordeling",
            Status = FristStatus.Godkjent
        });
        await _t.Db.SaveChangesAsync();

        await _regler.OpprettAsync(new RegelInndata
        {
            Tittel = "En uke etter rammefordeling",
            Rundetyper = [Rundetype.Marsrunden],
            Tidfestingstype = Tidfestingstype.AnkerRelativ,
            AnkerLoep = "rammefordeling",
            AnkerOffsetDager = 7
        });
        var runde = await _kal.GenererRundeAsync(Rundetype.Marsrunden, 2027, "Dag");
        var g = (await _kal.HentGjoeremaalAsync(runde, null)).Single();
        Assert.False(g.VenterPaaAnker);
        Assert.Equal(new DateOnly(2027, 3, 27), g.Sorteringsdag);
    }

    [Fact]
    public async Task Tentativt_anker_gir_ikke_en_sikker_dagsfrist()
    {
        _t.Db.Frister.Add(new Frist
        {
            Tittel = "Tentativ rammefordeling", Dato = new DateOnly(2027, 3, 1), Budsjettaar = 2027,
            Loep = "rammefordeling", Status = FristStatus.Godkjent,
            Datopresisjon = Datopresisjon.Maaned, Datokvalifikator = Datokvalifikator.Medio
        });
        await _t.Db.SaveChangesAsync();
        var runde = await _kal.OpprettRundeAsync(Rundetype.Marsrunden, 2027, "Anna");
        var id = await _kal.OpprettGjoeremaalAsync(runde, new GjoeremaalInndata
        {
            Tittel = "Følge opp", Tidfestingstype = Tidfestingstype.AnkerRelativ,
            AnkerLoep = "rammefordeling", AnkerOffsetDager = 7
        });
        var g = Assert.Single(await _kal.HentGjoeremaalAsync(runde, null));
        Assert.False(g.VenterPaaAnker);
        Assert.Equal(new DateOnly(2027, 3, 22), g.Sorteringsdag);
        Assert.Equal(Datopresisjon.Maaned, (await _kal.HentForRedigeringAsync(id))!.Datopresisjon);
        Assert.Contains("tentativ", g.Tidsangivelse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("22.03.2027", g.Tidsangivelse);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(int.MaxValue)]
    public async Task Ugyldig_rundeaar_avvises_uten_lagring(int aar)
    {
        await Assert.ThrowsAsync<Valideringsfeil>(() => _kal.OpprettRundeAsync(Rundetype.Rnb, aar, "Anna"));
        await Assert.ThrowsAsync<Valideringsfeil>(() => _kal.GenererRundeAsync(Rundetype.Rnb, aar, "Anna"));
        Assert.Empty(await _kal.HentRunderAsync());
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task Ankerforskyvning_utenfor_kalenderen_gir_valideringsfeil_uten_tapt_oppgave(int offset)
    {
        _t.Db.Frister.Add(new Frist
        {
            Tittel = "Ramme", Dato = new DateOnly(2027, 3, 20), Budsjettaar = 2027,
            Loep = "ramme", Status = FristStatus.Godkjent
        });
        await _t.Db.SaveChangesAsync();
        var runde = await _kal.OpprettRundeAsync(Rundetype.Rnb, 2027, "Anna");
        var id = await _kal.HurtiglaggAsync(runde, "Behold denne");
        await Assert.ThrowsAsync<Valideringsfeil>(() => _kal.OppdaterGjoeremaalAsync(id, new GjoeremaalInndata
        {
            Tittel = "Nytt utkast", Tidfestingstype = Tidfestingstype.AnkerRelativ,
            AnkerLoep = "ramme", AnkerOffsetDager = offset
        }));
        Assert.Equal("Behold denne", Assert.Single(await _kal.HentGjoeremaalAsync(runde, null)).Tittel);
    }

    [Fact]
    public async Task Hurtiginnlegging_i_slettet_runde_gir_forstaaelig_valideringsfeil()
    {
        var runde = await _kal.OpprettRundeAsync(Rundetype.Rnb, 2027, "Anna");
        await _kal.SlettRundeAsync(runde);
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => _kal.HurtiglaggAsync(runde, "Oppgave"));
        Assert.Contains("Runden finnes ikke", feil.Message);
    }

    public void Dispose() => _t.Dispose();
}
