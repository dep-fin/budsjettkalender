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
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Samme globale lagringskontrakt mot SQLite og ekte SQL Server.</summary>
public abstract class FasemalKontrakt
{
    protected abstract Task<Fasedatabase> OpprettDatabaseAsync();
    public static FaseDto[] EndredeFaser() => [new("Tidlig plan", -16), new("Nytt forslag", -9), new("Vedtak", -2), new("Iverksetting", 1), new("Avslutning", 13)];

    [Fact]
    public async Task Ny_database_har_fem_felles_standardfaser()
    {
        await using var database = await OpprettDatabaseAsync();
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var mal = await tjeneste.HentAsync();
        Assert.NotEqual(Guid.Empty, mal.Versjon);
        Assert.Equal(new[] { "Forberedelser", "Utarbeidelse av budsjettforslag", "Fremleggelse og vedtak", "Gjennomføring og revidering", "Regnskap" }, mal.Faser.Select(f => f.Navn));
        Assert.Equal(new[] { -15, -10, -3, 0, 12 }, mal.Faser.Select(f => f.Startmaaned));
        Assert.Equal(15, mal.SisteMaanedInkludert);
    }

    [Fact]
    public async Task Lagret_mal_leses_i_ny_operasjon_med_ny_versjon_uten_aa_endre_fristdata()
    {
        await using var database = await OpprettDatabaseAsync();
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var historikk = await FasedataBevis.HentAsync(database.Fabrikk());
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var foer = await tjeneste.HentAsync();
        var lagret = await tjeneste.OppdaterAsync(foer.Versjon, EndredeFaser(), 17);
        Assert.NotEqual(foer.Versjon, lagret.Versjon);
        Assert.NotEqual(Guid.Empty, lagret.Versjon);
        Like(lagret, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
        Assert.Equal(EndredeFaser(), lagret.Faser);
        Assert.Equal(17, lagret.SisteMaanedInkludert);
        Assert.Equal(historikk, await FasedataBevis.HentAsync(database.Fabrikk()));
    }

    [Fact]
    public async Task Siste_fase_kan_vaere_en_maaned_og_navn_trimmes()
    {
        await using var database = await OpprettDatabaseAsync();
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var mal = await tjeneste.HentAsync();
        var faser = EndredeFaser();
        faser[0] = faser[0] with { Navn = "  Tidlig plan  " };
        var lagret = await tjeneste.OppdaterAsync(mal.Versjon, faser, 13);
        Assert.Equal("Tidlig plan", lagret.Faser[0].Navn);
        Assert.Equal(13, (await tjeneste.HentAsync()).SisteMaanedInkludert);
    }

    [Theory]
    [InlineData("tomt navn")]
    [InlineData("blankt navn")]
    [InlineData("null navn")]
    [InlineData("like grenser")]
    [InlineData("synkende grenser")]
    [InlineData("slutt foer start")]
    [InlineData("overloep")]
    [InlineData("fire faser")]
    public async Task Ugyldig_mal_bevarer_hele_malen_og_versjonen(string feil)
    {
        await using var database = await OpprettDatabaseAsync();
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var foer = await tjeneste.HentAsync();
        var faser = EndredeFaser();
        var slutt = 17;
        switch (feil)
        {
            case "tomt navn": faser[0] = faser[0] with { Navn = "" }; break;
            case "blankt navn": faser[1] = faser[1] with { Navn = " \t " }; break;
            case "null navn": faser[2] = faser[2] with { Navn = null! }; break;
            case "like grenser": faser[1] = faser[1] with { Startmaaned = -16 }; break;
            case "synkende grenser": faser[2] = faser[2] with { Startmaaned = -17 }; break;
            case "slutt foer start": slutt = 12; break;
            case "overloep": slutt = int.MaxValue; break;
            case "fire faser": faser = faser[..4]; break;
        }
        var unntak = await Assert.ThrowsAsync<FasemalValideringsunntak>(() => tjeneste.OppdaterAsync(foer.Versjon, faser, slutt));
        Assert.NotEmpty(unntak.Feil);
        Like(foer, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
        // Den mislykkede operasjonens sporing følger ikke neste kall.
        await tjeneste.OppdaterAsync(foer.Versjon, EndredeFaser(), 17);
    }

    [Fact]
    public async Task Gammel_fane_avvises_og_kan_lese_ny_versjon_foer_bevisst_nytt_forsok()
    {
        await using var database = await OpprettDatabaseAsync();
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var historikk = await FasedataBevis.HentAsync(database.Fabrikk());
        IFasemal gammel = new FasemalTjeneste(database.Fabrikk());
        var foer = await gammel.HentAsync();
        var ny = await new FasemalTjeneste(database.Fabrikk()).OppdaterAsync(foer.Versjon, EndredeFaser(), 17);
        var fejl = await Assert.ThrowsAsync<FasemalKonfliktunntak>(() => gammel.OppdaterAsync(foer.Versjon, foer.Faser, 15));
        Assert.Contains("nyere", fejl.Message);
        Like(ny, await gammel.HentAsync());
        Assert.Equal(historikk, await FasedataBevis.HentAsync(database.Fabrikk()));
        var bevisst = await gammel.OppdaterAsync(ny.Versjon, foer.Faser, 15);
        Assert.Equal(foer.Faser, bevisst.Faser);
        Assert.NotEqual(ny.Versjon, bevisst.Versjon);
    }

    [Fact]
    public async Task To_operasjoner_som_har_lest_samme_versjon_har_bare_en_vinner()
    {
        await using var database = await OpprettDatabaseAsync();
        var foer = await new FasemalTjeneste(database.Fabrikk()).HentAsync();
        var a = new Lagringsport();
        var b = new Lagringsport();
        IFasemal forsteTjeneste = new FasemalTjeneste(database.Fabrikk(a));
        IFasemal andreTjeneste = new FasemalTjeneste(database.Fabrikk(b));
        var forste = forsteTjeneste.OppdaterAsync(foer.Versjon, EndredeFaser(), 17);
        var andre = andreTjeneste.OppdaterAsync(foer.Versjon, foer.Faser, 15);
        try
        {
            await Task.WhenAll(a.Ankommet, b.Ankommet).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotSame(a.Kontekst, b.Kontekst);
            Assert.NotSame(a.Tilkobling, b.Tilkobling);
            a.Slipp();
            var vinner = await forste.WaitAsync(TimeSpan.FromSeconds(20));
            b.Slipp();
            await Assert.ThrowsAsync<FasemalKonfliktunntak>(() => andre.WaitAsync(TimeSpan.FromSeconds(20)));
            Like(vinner, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
            var ny = await andreTjeneste.HentAsync();
            await andreTjeneste.OppdaterAsync(ny.Versjon, foer.Faser, 15);
        }
        finally
        {
            a.Slipp(); b.Slipp();
            try { await Task.WhenAll(forste, andre).WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { /* Frigjør begge operasjoner uten å skjule testresultatet. */ }
        }
    }

    [Fact]
    public async Task Lagringsfeil_bevarer_hel_mal_versjon_og_fristdata_og_tillater_nytt_forsok()
    {
        await using var database = await OpprettDatabaseAsync();
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var historikk = await FasedataBevis.HentAsync(database.Fabrikk());
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var foer = await tjeneste.HentAsync();
        await database.InstallerLagringsfeilAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => tjeneste.OppdaterAsync(foer.Versjon, EndredeFaser(), 17));
        Like(foer, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
        Assert.Equal(historikk, await FasedataBevis.HentAsync(database.Fabrikk()));
        await database.FjernLagringsfeilAsync();
        var lagret = await tjeneste.OppdaterAsync(foer.Versjon, EndredeFaser(), 17);
        Like(lagret, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
    }

    public static void Like(FasemalDto forventet, FasemalDto faktisk)
    {
        Assert.Equal(forventet.Versjon, faktisk.Versjon);
        Assert.Equal(forventet.Faser, faktisk.Faser);
        Assert.Equal(forventet.SisteMaanedInkludert, faktisk.SisteMaanedInkludert);
    }

    private sealed class Lagringsport : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _frigitt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ankommet => _ankommet.Task;
        public AppDbContext? Kontekst { get; private set; }
        public System.Data.Common.DbConnection? Tilkobling { get; private set; }
        public void Slipp() => _frigitt.TrySetResult();
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Kontekst = (AppDbContext)eventData.Context!;
            await Kontekst.Database.OpenConnectionAsync(cancellationToken);
            Tilkobling = Kontekst.Database.GetDbConnection();
            _ankommet.TrySetResult();
            await _frigitt.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }
}

public abstract class Fasedatabase : IAsyncDisposable
{
    protected abstract DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port);
    public IDbContextFactory<AppDbContext> Fabrikk(SaveChangesInterceptor? port = null) => new FabrikkImpl(Alternativer(port));
    public abstract Task InstallerLagringsfeilAsync();
    public abstract Task FjernLagringsfeilAsync();
    public abstract ValueTask DisposeAsync();
    private sealed class FabrikkImpl(DbContextOptions<AppDbContext> alternativer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(alternativer);
    }
}

