using Aarshjul.Application;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Internkalender;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aarshjul.Testkontrakter;

/// <summary>Samme offentlige synkvern mot SQLite og ekte, migrert SQL Server.</summary>
public abstract class InternkalenderSynkKontrakt
{
    protected abstract Task<Internkalenderdatabase> OpprettDatabaseAsync();
    private static IInternkalender Kalender(Internkalenderdatabase db, SaveChangesInterceptor? port = null)
        => new InternkalenderTjeneste(db.Fabrikk(port), new Klokke());
    private static IGjoeremaalRegler Regler(Internkalenderdatabase db) => new GjoeremaalRegelTjeneste(db.Fabrikk());
    private static RegelInndata Regel(string tittel = "Opprinnelig") => new()
    {
        Tittel = tittel, Notat = "Regelnotat", Rundetyper = [Rundetype.Rnb],
        Tidfestingstype = Tidfestingstype.KonkretDato, Maaned = 4, Dag = 20,
        Ansvarlige = [new("sbr-a", "Anna"), new(null, "Ekstern")]
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Arbeid_beskyttes_ogsaa_naar_det_lagres_etter_synkens_siste_lesing(bool fjern, bool fullfoer)
    {
        await using var db = await OpprettDatabaseAsync();
        var regler = Regler(db);
        var kal = Kalender(db);
        var regel = await regler.OpprettAsync(Regel());
        var runde = await kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Anna");
        var oppgave = Assert.Single(await kal.HentGjoeremaalAsync(runde, null));
        if (fjern) await regler.SlettAsync(regel);
        else await regler.OppdaterAsync(regel, Regel("Ny regel"));
        var forslag = await kal.ForberedSynkAsync(runde);
        var sistSynk = (await kal.HentRundeAsync(runde))!.SistSynkronisert;
        var port = new Lagringsport();
        var synk = Kalender(db, port).SynkroniserAsync(runde, forslag);
        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            if (fullfoer) await kal.FullfoerAsync(oppgave.Id, "sbr-b", "Bjørn");
            else
            {
                var redigering = (await kal.HentForRedigeringAsync(oppgave.Id))!;
                redigering.Tittel = "Eget utført arbeid";
                redigering.Notat = "Behold notat";
                redigering.Ansvarlige = [new("sbr-b", "Bjørn"), new(null, "Fagkontakt")];
                await kal.OppdaterGjoeremaalAsync(oppgave.Id, redigering);
            }
            port.Slipp();
            await Assert.ThrowsAsync<Valideringsfeil>(() => synk);
            var lagret = Assert.Single(await Kalender(db).HentGjoeremaalAsync(runde, null));
            Assert.Equal(oppgave.Id, lagret.Id);
            Assert.Equal(sistSynk, (await kal.HentRundeAsync(runde))!.SistSynkronisert);
            if (fullfoer)
            {
                Assert.Equal(GjoeremaalStatus.Fullfoert, lagret.Status);
                Assert.Equal("Bjørn", lagret.FullfoertAvNavn);
                Assert.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), lagret.FullfoertTid);
                Assert.Equal("Opprinnelig", lagret.Tittel);
                Assert.Equal("Regelnotat", lagret.Notat);
                Assert.Equal(2, lagret.Ansvarlige.Count);
            }
            else
            {
                Assert.True(lagret.ManueltEndret);
                Assert.Equal("Eget utført arbeid", lagret.Tittel);
                Assert.Equal("Behold notat", lagret.Notat);
                Assert.Contains(new AnsvarligDto("sbr-b", "Bjørn"), lagret.Ansvarlige);
                Assert.Contains(new AnsvarligDto(null, "Fagkontakt"), lagret.Ansvarlige);
            }
            Assert.Empty(await kal.ForberedSynkAsync(runde));
        }
        finally { port.Slipp(); try { await synk; } catch { /* Resultatet er kontrollert ovenfor. */ } }
    }

    [Fact]
    public async Task Samtidig_dobbel_synk_gir_bare_ett_nytt_gjoeremaal()
    {
        await using var db = await OpprettDatabaseAsync();
        var kal = Kalender(db);
        var runde = await kal.OpprettRundeAsync(Rundetype.Rnb, 2027, "Anna");
        await Regler(db).OpprettAsync(Regel());
        var forslag = await kal.ForberedSynkAsync(runde);
        var port = new Lagringsport();
        var gammel = Kalender(db, port).SynkroniserAsync(runde, forslag);
        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            await kal.SynkroniserAsync(runde, forslag);
            port.Slipp();
            await Assert.ThrowsAsync<Valideringsfeil>(() => gammel);
            Assert.Single(await Kalender(db).HentGjoeremaalAsync(runde, null));
            Assert.Empty(await kal.ForberedSynkAsync(runde));
        }
        finally { port.Slipp(); try { await gammel; } catch { } }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Samtidig_oppretting_bevarer_en_runde_og_staaende_Ovrig(int inngang)
    {
        await using var db = await OpprettDatabaseAsync();
        await Regler(db).OpprettAsync(Regel());
        static Task<Guid> Opprett(IInternkalender kal, int valg) => valg switch
        {
            0 => kal.OpprettRundeAsync(Rundetype.Rnb, 2027, "Anna"),
            1 => kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Anna"),
            _ => kal.HentEllerOpprettOvrigAsync("Anna")
        };
        var port = new Lagringsport();
        var gammel = Opprett(Kalender(db, port), inngang);
        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            var ny = await Opprett(Kalender(db), inngang);
            port.Slipp();
            if (inngang == 2) Assert.Equal(ny, await gammel);
            else await Assert.ThrowsAsync<Valideringsfeil>(() => gammel);
            Assert.Equal(ny, Assert.Single(await Kalender(db).HentRunderAsync()).Id);
            Assert.Equal(inngang == 1 ? 1 : 0, (await Kalender(db).HentGjoeremaalAsync(ny, null)).Count);
        }
        finally { port.Slipp(); try { await gammel; } catch { } }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Nyere_regelinnhold_eller_fjernet_rundetype_krever_ny_forhaandsvisning(int endring)
    {
        await using var db = await OpprettDatabaseAsync();
        var kal = Kalender(db);
        var regler = Regler(db);
        var regel = await regler.OpprettAsync(Regel());
        var runde = await kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Anna");
        var opprinnelig = Assert.Single(await kal.HentGjoeremaalAsync(runde, null));
        await regler.OppdaterAsync(regel, Regel("Forhåndsvist"));
        var gamle = await kal.ForberedSynkAsync(runde);
        if (endring == 3) await regler.SlettAsync(regel);
        else
        {
            var ny = Regel("Forhåndsvist");
            if (endring == 0) ny.Notat = "Nyere notat uten tittelendring";
            if (endring == 1) ny.Ansvarlige = [new("sbr-b", "Bjørn")];
            if (endring == 2) ny.Rundetyper = [Rundetype.Regnskap];
            await regler.OppdaterAsync(regel, ny);
        }
        var feil = await Assert.ThrowsAsync<Valideringsfeil>(() => kal.SynkroniserAsync(runde, gamle));
        Assert.Contains("ny forhåndsvisning", feil.Message);
        var beholdt = Assert.Single(await kal.HentGjoeremaalAsync(runde, null));
        Assert.Equal(opprinnelig.Id, beholdt.Id);
        Assert.Equal("Opprinnelig", beholdt.Tittel);
        var friske = await kal.ForberedSynkAsync(runde);
        await kal.SynkroniserAsync(runde, friske);
        if (endring >= 2) Assert.Empty(await kal.HentGjoeremaalAsync(runde, null));
        else
        {
            var oppdatert = Assert.Single(await kal.HentGjoeremaalAsync(runde, null));
            Assert.Equal(opprinnelig.Id, oppdatert.Id);
            Assert.Equal("Forhåndsvist", oppdatert.Tittel);
            if (endring == 0) Assert.Equal("Nyere notat uten tittelendring", oppdatert.Notat);
            else Assert.Equal(new AnsvarligDto("sbr-b", "Bjørn"), Assert.Single(oppdatert.Ansvarlige));
        }
    }

    [Fact]
    public async Task Valgt_delmengde_og_dobbel_utfoering_bevarer_identitet_og_uberoerte_forslag()
    {
        await using var db = await OpprettDatabaseAsync();
        var kal = Kalender(db);
        var regler = Regler(db);
        var oppdater = await regler.OpprettAsync(Regel());
        var fjern = await regler.OpprettAsync(Regel("Fjernes"));
        var runde = await kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Anna");
        var opprinnelig = (await kal.HentGjoeremaalAsync(runde, null)).Single(g => g.Tittel == "Opprinnelig");
        await regler.OppdaterAsync(oppdater, Regel("Oppdatert"));
        await regler.SlettAsync(fjern);
        await regler.OpprettAsync(Regel("Ny"));
        var foer = (await kal.HentRundeAsync(runde))!.SistSynkronisert;
        var forslag = await kal.ForberedSynkAsync(runde);
        Assert.Equal(3, forslag.Count);
        Assert.Equal(foer, (await kal.HentRundeAsync(runde))!.SistSynkronisert);
        var valgt = forslag.Where(f => f.Handling != SynkHandling.Fjern).ToArray();
        await kal.SynkroniserAsync(runde, valgt);
        var lagret = await kal.HentGjoeremaalAsync(runde, null);
        Assert.Equal(3, lagret.Count);
        Assert.Equal("Oppdatert", lagret.Single(g => g.Id == opprinnelig.Id).Tittel);
        Assert.Single(lagret, g => g.Tittel == "Ny");
        Assert.Single(lagret, g => g.Tittel == "Fjernes");
        await Assert.ThrowsAsync<Valideringsfeil>(() => kal.SynkroniserAsync(runde, valgt));
        Assert.Equal(3, (await kal.HentGjoeremaalAsync(runde, null)).Count);
        Assert.Equal(SynkHandling.Fjern, Assert.Single(await kal.ForberedSynkAsync(runde)).Handling);
    }

    [Fact]
    public async Task Regelendring_etter_synkens_siste_lesing_vises_i_neste_forhaandsvisning()
    {
        await using var db = await OpprettDatabaseAsync();
        var kal = Kalender(db);
        var regler = Regler(db);
        var regel = await regler.OpprettAsync(Regel());
        var runde = await kal.GenererRundeAsync(Rundetype.Rnb, 2027, "Anna");
        await regler.OppdaterAsync(regel, Regel("Valgt snapshot"));
        var forslag = await kal.ForberedSynkAsync(runde);
        var port = new Lagringsport();
        var synk = Kalender(db, port).SynkroniserAsync(runde, forslag);
        try
        {
            await port.Ankommet.WaitAsync(TimeSpan.FromSeconds(20));
            await regler.OppdaterAsync(regel, Regel("Nyere regel under lagring"));
            port.Slipp();
            await synk;
            Assert.Equal("Valgt snapshot", Assert.Single(await kal.HentGjoeremaalAsync(runde, null)).Tittel);
            // Utføring er et snapshot, ikke en lås mot regelredigering i andre runder.
            // Derfor må UI lese forhåndsvisningen på nytt før den viser at det ikke finnes endringer.
            Assert.Equal("Nyere regel under lagring", Assert.Single(await kal.ForberedSynkAsync(runde)).Tittel);
        }
        finally { port.Slipp(); try { await synk; } catch { } }
    }

    private sealed class Klokke : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    }
    private sealed class Lagringsport : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _slipp = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Ankommet => _ankommet.Task;
        public void Slipp() => _slipp.TrySetResult();
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            _ankommet.TrySetResult();
            await _slipp.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            return result;
        }
    }
}

public abstract class Internkalenderdatabase : IAsyncDisposable
{
    protected abstract DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port);
    public IDbContextFactory<AppDbContext> Fabrikk(SaveChangesInterceptor? port = null) => new FabrikkImpl(Alternativer(port));
    public abstract ValueTask DisposeAsync();
    private sealed class FabrikkImpl(DbContextOptions<AppDbContext> alternativer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(alternativer);
    }
}
