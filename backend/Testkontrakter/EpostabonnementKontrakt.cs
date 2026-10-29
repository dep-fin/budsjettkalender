using System.Collections.Concurrent;
using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Samme offentlige påmeldingskontrakt mot SQLite og ekte SQL Server.</summary>
public abstract class EpostabonnementKontrakt
{
    [Fact]
    public async Task Adressebytte_bevarer_en_rad_opprettelsestid_kvote_og_grenser_til_ny_full_aktivering()
    {
        await using var database = await OpprettAsync(); var p = new Proeve(database); await p.MeldPaa();
        var status = await p.Tjeneste.BekreftAsync("a", p.Token);
        status = await p.Tjeneste.EndreValgAsync("a", true, true, status.Versjon, [2031], [Kategori.Budsjett]);
        status = await p.Tjeneste.EndreValgAsync("a", true, true, status.Versjon, [2031, 2032], [Kategori.Budsjett]);
        await using var db = database.Fabrikk().CreateDbContext();
        var foer = await db.Epostabonnementer.AsNoTracking().SingleAsync();
        async Task<long[]> Grenser() => [
            await db.Database.SqlQueryRaw<long>("SELECT Sekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'a'").SingleAsync(),
            await db.Database.SqlQueryRaw<long>("SELECT Endringssekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'a'").SingleAsync(),
            await db.Database.SqlQueryRaw<long>("SELECT Livssyklussekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'a'").SingleAsync()];
        var gamleGrenser = await Grenser(); p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        await p.Tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, status.Versjon);
        var venter = await db.Epostabonnementer.AsNoTracking().SingleAsync();
        Assert.Equal("ny@jd.dep.no", venter.Adresse); Assert.Equal(foer.Opprettet, venter.Opprettet);
        Assert.Equal(foer.SendevinduStart, venter.SendevinduStart); Assert.Equal(2, venter.AntallSendt);
        Assert.Equal(foer.UtvalgJson, venter.UtvalgJson); Assert.Equal(gamleGrenser, await Grenser());
        await p.Tjeneste.BekreftAsync("a", p.Token); var nyeGrenser = await Grenser();
        Assert.True(nyeGrenser[0] > gamleGrenser.Max()); Assert.Equal(nyeGrenser[0], nyeGrenser[1]); Assert.Equal(nyeGrenser[0], nyeGrenser[2]);
        var aktiv = await db.Epostabonnementer.AsNoTracking().SingleAsync();
        Assert.Equal(foer.Opprettet, aktiv.Opprettet); Assert.Equal(2, aktiv.AntallSendt);
        using var utvalg = System.Text.Json.JsonDocument.Parse(aktiv.UtvalgJson!);
        var klausul = Assert.Single(utvalg.RootElement.GetProperty("Grenser").EnumerateArray());
        Assert.Equal(nyeGrenser[0], klausul.GetProperty("Sekvens").GetInt64());
        Assert.Equal(new[] { 2031, 2032 }, klausul.GetProperty("Budsjettaar").EnumerateArray().Select(x => x.GetInt32()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Samme_normaliserte_adresse_beholder_status_versjon_og_bekreftelse_uten_post(bool aktiv)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); var status = await p.MeldPaa();
        if (aktiv) status = await p.Tjeneste.BekreftAsync("a", p.Token);
        var uendret = await p.Tjeneste.EndreAdresseAsync("a", "egen@FIN.DEP.NO", true, status.Versjon);
        Assert.Equal(status, uendret); Assert.Single(p.Post.Meldinger);
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => p.Tjeneste.EndreAdresseAsync("a", "egen@fin.dep.no", true, Guid.Empty));
        if (!aktiv) Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    [Theory]
    [InlineData("ny@dep.no", true)]
    [InlineData("ny@fin.dep.no.example.org", true)]
    [InlineData("a@fin.dep.no,b@jd.dep.no", true)]
    [InlineData("ny@fin.dep.no\r\nBcc:x@jd.dep.no", true)]
    [InlineData("ny@jd.dep.no", false)]
    public async Task Ugyldig_adressebytte_eller_manglende_egenadresse_samtykke_endrer_ingenting(string adresse, bool samtykke)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); await p.MeldPaa();
        var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token); p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreAdresseAsync("a", adresse, samtykke, aktiv.Versjon));
        Assert.Equal(aktiv, await p.NyTjeneste().HentAsync("a")); Assert.Single(p.Post.Meldinger);
    }

    [Fact]
    public async Task Adressebytte_krever_eksisterende_eget_abonnement_og_tilgjengelig_bekreftelsestransport()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, Guid.Empty));
        await p.MeldPaa(); var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreAdresseAsync("annen", "ny@jd.dep.no", true, aktiv.Versjon));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        IEpostabonnement stengt = new EpostabonnementTjeneste(db.Fabrikk(), p.Klokke, new UtilgjengeligBekreftelsespost());
        await Assert.ThrowsAsync<Valideringsfeil>(() => stengt.EndreAdresseAsync("a", "ny@jd.dep.no", true, aktiv.Versjon));
        Assert.Equal(aktiv, await p.NyTjeneste().HentAsync("a")); Assert.Single(p.Post.Meldinger);
    }

    [Fact]
    public async Task Adressebytte_bevarer_minuttpause_og_felles_timekvote_gjennom_flere_adresser_og_nyutsending()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); var status = await p.MeldPaa();
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, status.Versjon));
        for (var i = 0; i < 4; i++)
        {
            p.Klokke.Flytt(TimeSpan.FromMinutes(1));
            status = await p.Tjeneste.EndreAdresseAsync("a", $"ny{i}@jd.dep.no", true, status.Versjon);
        }
        p.Klokke.Flytt(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().EndreAdresseAsync("a", "stopp@jd.dep.no", true, status.Versjon));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().SendNyAsync("a"));
        Assert.Equal(5, p.Post.Meldinger.Count); Assert.Equal(status, await p.Tjeneste.HentAsync("a"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(55));
        await p.Tjeneste.EndreAdresseAsync("a", "siste@jd.dep.no", true, status.Versjon);
        Assert.Equal(6, p.Post.Meldinger.Count);
        p.Klokke.Flytt(TimeSpan.FromMinutes(15));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", p.Token));
        await p.NyTjeneste().SendNyAsync("a"); Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    [Fact]
    public async Task Sendefeil_etter_adressebytte_bevarer_ny_ventende_uten_gammel_aktiv_adresse()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); await p.MeldPaa();
        var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token); var gammel = p.Token;
        p.Klokke.Flytt(TimeSpan.FromMinutes(1)); p.Post.Feil = true;
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, aktiv.Versjon));
        Assert.Equal("Bekreftelsesmeldingen kunne ikke sendes. Vent ett minutt og be om en ny lenke.", feil.Message);
        var venter = await p.NyTjeneste().HentAsync("a");
        Assert.Equal("VenterPaaBekreftelse", venter.Status); Assert.Null(venter.Aktivert); Assert.Equal("ny@jd.dep.no", venter.Adresse);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", gammel));
        p.Post.Feil = false;
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.SendNyAsync("a"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(1)); await p.Tjeneste.SendNyAsync("a");
        Assert.Equal("ny@jd.dep.no", (await p.Tjeneste.BekreftAsync("a", p.Token)).Adresse);
    }

    [Theory]
    [InlineData("adresse")]
    [InlineData("ny-lenke")]
    [InlineData("valg")]
    [InlineData("bekreft")]
    [InlineData("avmelding")]
    public async Task Eldre_adressebytte_kan_ikke_overskrive_samtidig_vinner_eller_gjenopplive_avmelding(string operasjon)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); var status = await p.MeldPaa();
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        using var port = new Lagringsport(1, manuell: true);
        var gammel = p.NyTjeneste(port).EndreAdresseAsync("a", "taper@jd.dep.no", true, status.Versjon);
        try
        {
            await port.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var vinner = await Vinn(p, operasjon, status);
            var antall = p.Post.Meldinger.Count;
            port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => gammel);
            Assert.Equal(vinner, await p.NyTjeneste().HentAsync("a")); Assert.Equal(antall, p.Post.Meldinger.Count);
            await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => p.Tjeneste.EndreAdresseAsync("a", "stale@jd.dep.no", true, status.Versjon));
        }
        finally { port.Fortsett.TrySetResult(); try { await gammel; } catch (EpostabonnementKonflikt) { } }
    }

    [Theory]
    [InlineData("ny-lenke")]
    [InlineData("valg")]
    [InlineData("bekreft")]
    [InlineData("avmelding")]
    public async Task Adressebytte_vinner_over_eldre_bekreftelse_nyutsending_valg_eller_avmelding(string operasjon)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); var status = await p.MeldPaa(); var token = p.Token;
        p.Klokke.Flytt(TimeSpan.FromMinutes(2)); using var port = new Lagringsport(1, manuell: true);
        var gammel = Vinn(p, operasjon, status, p.NyTjeneste(port));
        try
        {
            await port.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var vinner = await p.Tjeneste.EndreAdresseAsync("a", "vinner@jd.dep.no", true, status.Versjon);
            port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => gammel);
            Assert.Equal(vinner, await p.NyTjeneste().HentAsync("a")); Assert.Equal(2, p.Post.Meldinger.Count);
            await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", token));
            Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
        }
        finally { port.Fortsett.TrySetResult(); try { await gammel; } catch (EpostabonnementKonflikt) { } }
    }

    [Theory]
    [InlineData("adresse")]
    [InlineData("ny-lenke")]
    [InlineData("valg")]
    [InlineData("bekreft")]
    [InlineData("avmelding")]
    public async Task Forsinket_transportsvar_fra_adressebytte_returnerer_faktisk_vinnerstatus(string operasjon)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); var status = await p.MeldPaa();
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        var mottatt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var antall = 0;
        p.Post.EtterOverlevering = async ct =>
        {
            if (Interlocked.Increment(ref antall) != 1) return;
            mottatt.TrySetResult(); await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        };
        var bytte = p.Tjeneste.EndreAdresseAsync("a", "mellom@jd.dep.no", true, status.Versjon);
        try
        {
            await mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15)); p.Klokke.Flytt(TimeSpan.FromMinutes(1));
            var vinner = await Vinn(p, operasjon, await p.Tjeneste.HentAsync("a"));
            fortsett.TrySetResult(); Assert.Equal(vinner, await bytte); Assert.Equal(vinner, await p.NyTjeneste().HentAsync("a"));
        }
        finally { fortsett.TrySetResult(); }
    }

    private static Task<EpostabonnementDto> Vinn(Proeve p, string operasjon, EpostabonnementDto status, IEpostabonnement? tjeneste = null)
        => operasjon switch
        {
            "adresse" => (tjeneste ?? p.Tjeneste).EndreAdresseAsync("a", "vinner@jd.dep.no", true, status.Versjon),
            "ny-lenke" => (tjeneste ?? p.Tjeneste).SendNyAsync("a"),
            "valg" => (tjeneste ?? p.Tjeneste).EndreValgAsync("a", false, true, status.Versjon),
            "bekreft" => (tjeneste ?? p.Tjeneste).BekreftAsync("a", p.Token),
            _ => (tjeneste ?? p.Tjeneste).MeldAvAsync("a")
        };

    [Theory]
    [InlineData("Aktiv")]
    [InlineData("VenterPaaBekreftelse")]
    [InlineData("Avmeldt")]
    public async Task Adressebytte_stanser_varsling_og_bevarer_egne_valg_varig_til_ny_engangsbekreftelse(string startstatus)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        var status = await p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true, false, true, [2031, 2032], [Kategori.Regnskap]);
        var gammel = p.Token;
        if (startstatus == "Aktiv") status = await p.Tjeneste.BekreftAsync("a", gammel);
        if (startstatus == "Avmeldt") status = await p.Tjeneste.MeldAvAsync("a");
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        status = await p.NyTjeneste().EndreAdresseAsync("a", "ny@JD.DEP.NO", true, status.Versjon);
        Assert.Equal("VenterPaaBekreftelse", status.Status); Assert.Null(status.Aktivert);
        Assert.Equal("ny@jd.dep.no", status.Adresse); Assert.False(status.NyeFrister); Assert.True(status.VesentligeEndringer);
        Assert.Equal(new[] { 2031, 2032 }, status.Budsjettaar); Assert.Equal(new[] { Kategori.Regnskap }, status.Kategorier);
        var lagret = await p.NyTjeneste().HentAsync("a");
        Assert.Equal(status.Versjon, lagret.Versjon); Assert.Equal(status.Status, lagret.Status); Assert.Equal(status.Adresse, lagret.Adresse);
        Assert.Equal(2, p.Post.Meldinger.Count);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", gammel));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("annen", p.Token));
        var aktiv = await p.NyTjeneste().BekreftAsync("a", p.Token);
        Assert.Equal("Aktiv", aktiv.Status); Assert.Equal(p.Klokke.GetUtcNow(), aktiv.Aktivert);
        Assert.Equal("ny@jd.dep.no", aktiv.Adresse); Assert.Equal(new[] { 2031, 2032 }, aktiv.Budsjettaar);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", p.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tomt_og_null_utvalg_betyr_Alle_og_konkrete_valg_normaliseres_varig(bool tomt)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        var status = await p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true, true, false,
            tomt ? [] : null, tomt ? [] : null);
        Assert.True(status.AlleBudsjettaar); Assert.True(status.AlleKategorier);
        Assert.Null(status.Budsjettaar); Assert.Null(status.Kategorier);
        status = await p.Tjeneste.BekreftAsync("a", p.Token);
        status = await p.NyTjeneste().EndreValgAsync("a", true, true, status.Versjon,
            [2032, 2031, 2032], [Kategori.Regnskap, Kategori.Budsjett, Kategori.Regnskap]);
        var lagret = await p.NyTjeneste().HentAsync("a");
        Assert.False(lagret.AlleBudsjettaar); Assert.False(lagret.AlleKategorier);
        Assert.Equal(new[] { 2031, 2032 }, lagret.Budsjettaar);
        Assert.Equal(new[] { Kategori.Budsjett, Kategori.Regnskap }, lagret.Kategorier);
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => p.Tjeneste.EndreValgAsync("a", true, false, Guid.Empty, null, null));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreValgAsync("anden", true, false, status.Versjon, null, null));
        // EV04s type-only-kall bevarer abonnementets egne utvalg.
        status = await p.Tjeneste.EndreValgAsync("a", true, false, status.Versjon);
        Assert.Equal(new[] { 2031, 2032 }, status.Budsjettaar);
        status = await p.Tjeneste.EndreValgAsync("a", true, false, status.Versjon, [], []);
        Assert.True(status.AlleBudsjettaar); Assert.True(status.AlleKategorier);
        await p.Tjeneste.MeldAvAsync("a");
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreValgAsync("a", true, false, status.Versjon, [2031], []));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(10000, 0)]
    [InlineData(2031, -1)]
    [InlineData(2031, 3)]
    public async Task Ugyldig_utvalg_avvises_foer_lagring_eller_bekreftelsespost(int aar, int kategori)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true, true, false, [aar], [(Kategori)kategori]));
        Assert.Empty(p.Post.Meldinger); Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("a")).Status);
        await p.MeldPaa(); var status = await p.Tjeneste.BekreftAsync("a", p.Token);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.EndreValgAsync("a", true, false, status.Versjon, [aar], [(Kategori)kategori]));
        Assert.Equal(status, await p.NyTjeneste().HentAsync("a"));
    }

    [Fact]
    public async Task Samtidige_filterlagringer_har_en_vinner_uten_fletting_av_ulike_utvalg()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); await p.MeldPaa();
        var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token);
        using var port = new Lagringsport(1, manuell: true);
        var gammel = p.NyTjeneste(port).EndreValgAsync("a", true, false, aktiv.Versjon, [2031], [Kategori.Budsjett]);
        try
        {
            await port.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await p.Tjeneste.EndreValgAsync("a", true, false, aktiv.Versjon, [2032], [Kategori.Regnskap]);
            port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => gammel);
            var lagret = await p.NyTjeneste().HentAsync("a");
            Assert.Equal(new[] { 2032 }, lagret.Budsjettaar); Assert.Equal(new[] { Kategori.Regnskap }, lagret.Kategorier);
        }
        finally { port.Fortsett.TrySetResult(); try { await gammel; } catch (EpostabonnementKonflikt) { } }
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Varselvalg_bekreftes_og_endres_varig_bare_med_gjeldende_versjon(bool nye, bool endringer)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true, nye, endringer);
        var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token);
        Assert.Equal(nye, aktiv.NyeFrister); Assert.Equal(endringer, aktiv.VesentligeEndringer);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().EndreValgAsync("a", false, false, aktiv.Versjon));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().EndreValgAsync("annen", true, true, aktiv.Versjon));
        var endret = await p.NyTjeneste().EndreValgAsync("a", !nye || !endringer, true, aktiv.Versjon);
        Assert.Equal(endret, await p.NyTjeneste().HentAsync("a"));
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => p.NyTjeneste().EndreValgAsync("a", true, false, aktiv.Versjon));
        Assert.Equal(endret, await p.NyTjeneste().HentAsync("a"));
    }
    protected abstract Task<Epostdatabase> OpprettAsync();

    [Fact]
    public async Task Ingen_varselvalg_avvises_foer_paamelding_eller_bekreftelsesmelding()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true, false, false));
        Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("a")).Status); Assert.Empty(p.Post.Meldinger);
    }

    [Fact]
    public async Task Eldre_innstillingsendring_kan_ikke_overskrive_en_samtidig_avmelding()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db); await p.MeldPaa();
        var aktiv = await p.Tjeneste.BekreftAsync("a", p.Token);
        using var port = new Lagringsport(1, manuell: true);
        var ventende = p.NyTjeneste(port).EndreValgAsync("a", true, true, aktiv.Versjon);
        try
        {
            await port.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15)); await p.Tjeneste.MeldAvAsync("a");
            port.Fortsett.TrySetResult(); await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => ventende);
            Assert.Equal("Avmeldt", (await p.NyTjeneste().HentAsync("a")).Status);
        }
        finally { port.Fortsett.TrySetResult(); try { await ventende; } catch (EpostabonnementKonflikt) { } }
    }

    [Fact]
    public async Task Ingen_automatisk_paamelding_og_eksplisitte_standarder_bevares_i_ny_tjeneste()
    {
        await using var db = await OpprettAsync();
        var p = new Proeve(db);
        Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("a")).Status);
        Assert.Empty(p.Post.Meldinger);
        await p.Tjeneste.MeldPaaAsync("a", "egen@FIN.DEP.NO", true);
        var lagret = await p.NyTjeneste().HentAsync("a");
        Assert.Equal(new EpostabonnementDto("VenterPaaBekreftelse", "egen@fin.dep.no", true, false, true, true, null, p.Klokke.GetUtcNow(), lagret.Versjon), lagret);
        Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("annen")).Status);
        Assert.Single(p.Post.Meldinger);
    }

    [Fact]
    public async Task Egen_engangsbekreftelse_setter_varig_starttidspunkt_ved_aktivering()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var token = p.Token;
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("annen", token));
        p.Klokke.Flytt(TimeSpan.FromMinutes(3));
        var aktiv = await p.NyTjeneste().BekreftAsync("a", token);
        Assert.Equal("Aktiv", aktiv.Status);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 8, 3, 0, TimeSpan.Zero), aktiv.Aktivert);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", token));
        Assert.Equal(aktiv, await p.NyTjeneste().HentAsync("a"));
    }

    [Fact]
    public async Task Utloep_ved_femten_minutter_krever_ny_lenke_og_gammel_lenke_forblir_ugyldig()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var gammel = p.Token;
        p.Klokke.Flytt(TimeSpan.FromMinutes(15));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().BekreftAsync("a", gammel));
        Assert.Null((await p.Tjeneste.HentAsync("a")).Aktivert);
        await p.Tjeneste.SendNyAsync("a");
        Assert.True(gammel != p.Token, "Ny melding må ha en ny hemmelig bekreftelse.");
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", gammel));
        Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    [Fact]
    public async Task Fem_feilforsoek_bevares_mellom_operasjoner_og_sperrer_ogsaa_riktig_lenke()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var token = p.Token;
        for (var i = 0; i < 5; i++) await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().BekreftAsync("a", "feil"));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", token));
        Assert.Equal("VenterPaaBekreftelse", (await p.Tjeneste.HentAsync("a")).Status);
        p.Klokke.Flytt(TimeSpan.FromMinutes(1));
        await p.Tjeneste.SendNyAsync("a");
        Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    [Fact]
    public async Task Nyutsending_begrenses_varig_med_minuttpause_og_fem_meldinger_per_time()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var token = p.Token;
        await p.MeldPaa(); // Samme innsending er idempotent, også mens bekreftelse venter.
        Assert.Single(p.Post.Meldinger);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().SendNyAsync("a"));
        for (var i = 0; i < 4; i++) { p.Klokke.Flytt(TimeSpan.FromMinutes(1)); await p.NyTjeneste().SendNyAsync("a"); }
        p.Klokke.Flytt(TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.NyTjeneste().SendNyAsync("a"));
        Assert.Equal(5, p.Post.Meldinger.Count);
        p.Klokke.Flytt(TimeSpan.FromMinutes(55));
        await p.NyTjeneste().SendNyAsync("a");
        Assert.Equal(6, p.Post.Meldinger.Count);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", token));
        Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Avmelding_ugyldiggjoer_lenker_og_ny_eksplisitt_paamelding_faar_nytt_starttidspunkt(bool aktiv)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var gammel = p.Token;
        if (aktiv) await p.Tjeneste.BekreftAsync("a", gammel);
        await p.Tjeneste.MeldAvAsync("a");
        Assert.Equal("Avmeldt", (await p.NyTjeneste().HentAsync("a")).Status);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", gammel));
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.SendNyAsync("a"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        await p.MeldPaa();
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", gammel));
        Assert.Null((await p.Tjeneste.HentAsync("a")).Aktivert);
        Assert.Equal(p.Klokke.GetUtcNow(), (await p.Tjeneste.BekreftAsync("a", p.Token)).Aktivert);
    }

    [Fact]
    public async Task Samtidig_foerstegangspaamelding_oppretter_ett_abonnement_og_en_melding()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        using var port = new Lagringsport(2);
        var tjeneste = p.NyTjeneste(port);
        var svar = await Task.WhenAll(tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true), tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true));
        Assert.All(svar, s => Assert.Equal("VenterPaaBekreftelse", s.Status));
        Assert.Single(p.Post.Meldinger);
        Assert.Equal("Aktiv", (await p.NyTjeneste().BekreftAsync("a", p.Token)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Avmelding_vinner_over_eldre_bekreftelse_eller_nyutsending(bool sendNy)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var token = p.Token;
        p.Klokke.Flytt(TimeSpan.FromMinutes(1));
        using var port = new Lagringsport(1, manuell: true);
        var gammel = p.NyTjeneste(port);
        var ventende = sendNy ? gammel.SendNyAsync("a") : gammel.BekreftAsync("a", token);
        await port.Mottatt.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await p.Tjeneste.MeldAvAsync("a");
        port.Fortsett.TrySetResult();
        await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => ventende);
        Assert.Equal("Avmeldt", (await p.NyTjeneste().HentAsync("a")).Status);
        Assert.Single(p.Post.Meldinger);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", token));
    }

    [Fact]
    public async Task Samtidig_bekreftelse_kan_bare_aktivere_en_gang()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await p.MeldPaa(); var token = p.Token;
        using var port = new Lagringsport(2);
        var tjeneste = p.NyTjeneste(port);
        async Task<bool> Bekreft()
        {
            try { await tjeneste.BekreftAsync("a", token); return true; }
            catch (EpostabonnementKonflikt) { return false; }
        }
        var svar = await Task.WhenAll(Bekreft(), Bekreft());
        Assert.Single(svar, x => x);
        Assert.Equal("Aktiv", (await p.Tjeneste.HentAsync("a")).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Svar_etter_transportoverlevering_bevarer_samtidig_avmelding_eller_aktivering(bool aktiver)
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        var overlevert = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fortsett = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        p.Post.EtterOverlevering = async ct =>
        {
            overlevert.TrySetResult();
            await fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        };
        var paamelding = p.MeldPaa();
        try
        {
            await overlevert.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var vinner = aktiver
                ? await p.NyTjeneste().BekreftAsync("a", p.Token)
                : await p.NyTjeneste().MeldAvAsync("a");
            fortsett.TrySetResult();
            Assert.Equal(vinner, await paamelding);
            Assert.Equal(vinner, await p.NyTjeneste().HentAsync("a"));
            Assert.Single(p.Post.Meldinger);
            await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.BekreftAsync("a", p.Token));
        }
        finally { fortsett.TrySetResult(); }
    }

    [Fact]
    public async Task Eierskap_samtykke_og_adressebytte_haandheves_foer_noen_ny_melding()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", false));
        Assert.Empty(p.Post.Meldinger);
        await p.MeldPaa();
        await Assert.ThrowsAsync<Valideringsfeil>(() => p.Tjeneste.MeldPaaAsync("a", "annen@jd.dep.no", true));
        await p.Tjeneste.MeldAvAsync("annen");
        Assert.Equal("VenterPaaBekreftelse", (await p.Tjeneste.HentAsync("a")).Status);
        Assert.Single(p.Post.Meldinger);
    }

    [Fact]
    public async Task Utilgjengelig_transport_gir_ingen_paamelding_og_sendefeil_gir_ingen_aktivering()
    {
        await using var db = await OpprettAsync(); var p = new Proeve(db);
        IEpostabonnement stengt = new EpostabonnementTjeneste(db.Fabrikk(), p.Klokke, new UtilgjengeligBekreftelsespost());
        await Assert.ThrowsAsync<Valideringsfeil>(() => stengt.MeldPaaAsync("a", "egen@fin.dep.no", true));
        Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("a")).Status);
        p.Post.Feil = true;
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => p.MeldPaa());
        Assert.Equal("Bekreftelsesmeldingen kunne ikke sendes. Vent ett minutt og be om en ny lenke.", feil.Message);
        Assert.Null((await p.NyTjeneste().HentAsync("a")).Aktivert);
        Assert.Empty(p.Post.Meldinger);
        p.Post.Feil = false;
        p.Klokke.Flytt(TimeSpan.FromMinutes(1));
        await p.Tjeneste.SendNyAsync("a");
        Assert.Equal("Aktiv", (await p.Tjeneste.BekreftAsync("a", p.Token)).Status);
    }

    private sealed class Proeve(Epostdatabase database)
    {
        public readonly Proeveklokke Klokke = new();
        public readonly Testpost Post = new();
        public IEpostabonnement Tjeneste => NyTjeneste();
        public IEpostabonnement NyTjeneste(SaveChangesInterceptor? port = null) => new EpostabonnementTjeneste(database.Fabrikk(port), Klokke, Post);
        public Task<EpostabonnementDto> MeldPaa() => Tjeneste.MeldPaaAsync("a", "egen@fin.dep.no", true);
        public string Token => Post.Meldinger.Last().Token;
    }
    public sealed class Proeveklokke : TimeProvider
    {
        private DateTimeOffset _naa = new(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _naa;
        public void Flytt(TimeSpan tid) => _naa += tid;
    }
    private sealed class Testpost : IBekreftelsespost
    {
        public readonly ConcurrentQueue<Melding> Meldinger = new();
        public bool Feil;
        public Func<CancellationToken, Task>? EtterOverlevering;
        public bool Tilgjengelig => true;
        public Task SendAsync(string brukerId, string adresse, string token, CancellationToken ct = default)
        {
            if (Feil) throw new InvalidOperationException("Transportdetaljer med hemmelig innhold skal ikke returneres.");
            Meldinger.Enqueue(new Melding(token));
            return EtterOverlevering?.Invoke(ct) ?? Task.CompletedTask;
        }
    }
    private sealed class Melding(string token) { public string Token { get; } = token; }

    private sealed class Lagringsport(int antall, bool manuell = false) : SaveChangesInterceptor, IDisposable
    {
        private int _ankommet;
        public readonly TaskCompletionSource Mottatt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _ankommet) <= antall)
            {
                if (Volatile.Read(ref _ankommet) == antall) { Mottatt.TrySetResult(); if (!manuell) Fortsett.TrySetResult(); }
                await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
        public void Dispose() => Fortsett.TrySetResult();
    }
}

public abstract class Epostdatabase : IAsyncDisposable
{
    protected abstract DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port);
    public IDbContextFactory<AppDbContext> Fabrikk(SaveChangesInterceptor? port = null) => new FabrikkAdapter(Alternativer(port));
    public IDbContextFactory<AppDbContext> FabrikkMedKommando(DbCommandInterceptor port)
        => new FabrikkAdapter(new DbContextOptionsBuilder<AppDbContext>(Alternativer(null)).AddInterceptors(port).Options);
    public IDbContextFactory<AppDbContext> FabrikkMedPort(IInterceptor port)
        => new FabrikkAdapter(new DbContextOptionsBuilder<AppDbContext>(Alternativer(null)).AddInterceptors(port).Options);
    private sealed class FabrikkAdapter(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    { public AppDbContext CreateDbContext() => new(options); }
    public abstract ValueTask DisposeAsync();
}
