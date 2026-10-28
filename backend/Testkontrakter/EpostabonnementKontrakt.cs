using System.Collections.Concurrent;
using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Samme offentlige påmeldingskontrakt mot SQLite og ekte SQL Server.</summary>
public abstract class EpostabonnementKontrakt
{
    protected abstract Task<Epostdatabase> OpprettAsync();

    [Fact]
    public async Task Ingen_automatisk_paamelding_og_eksplisitte_standarder_bevares_i_ny_tjeneste()
    {
        await using var db = await OpprettAsync();
        var p = new Proeve(db);
        Assert.Equal("IkkePaameldt", (await p.Tjeneste.HentAsync("a")).Status);
        Assert.Empty(p.Post.Meldinger);
        await p.Tjeneste.MeldPaaAsync("a", "egen@FIN.DEP.NO", true);
        var lagret = await p.NyTjeneste().HentAsync("a");
        Assert.Equal(new EpostabonnementDto("VenterPaaBekreftelse", "egen@fin.dep.no", true, false, true, true, null, p.Klokke.GetUtcNow()), lagret);
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
    private sealed class FabrikkAdapter(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    { public AppDbContext CreateDbContext() => new(options); }
    public abstract ValueTask DisposeAsync();
}
