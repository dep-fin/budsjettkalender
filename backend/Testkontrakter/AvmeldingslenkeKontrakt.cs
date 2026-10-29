using Aarshjul.Application.Epost;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Infrastructure.Datautveksling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.RegularExpressions;
using Xunit;

namespace Aarshjul.Testkontrakter;

public abstract partial class DagligEpostKontrakt
{
    [Theory]
    [InlineData(true, false, false)] [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(true, false, true)] [InlineData(false, true, true)] [InlineData(true, true, true)]
    public async Task Lenkeavmelding_og_ny_aktivering_stanser_alle_hendelsestyper_uten_inaktivt_etterslep(bool nye, bool endringer, bool alle)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        var import = new FristDatautveksling(db.Fabrikk(), p.Klokke);
        var rader = new List<FristEksport>();
        foreach (var fase in new[] { "før avmelding", "under venting", "etter aktivering" })
        {
            rader.Add(Importrad("Endring " + fase)); rader.Add(Importrad("Sletting " + fase));
        }
        await import.ImporterAsync(new() { Frister = rader }); await p.Aktiver();
        rader.Add(Importrad("Faktisk tidligere post")); await import.ImporterAsync(new() { Frister = rader }); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a")));
        await VelgUtvalg(p, alle ? null : [2031], alle ? null : [Kategori.Budsjett], nye, endringer);
        async Task Publiser(string fase)
        {
            rader.Add(Importrad("Ny " + fase));
            var i = rader.FindIndex(r => r.Tittel == "Endring " + fase);
            rader[i] = rader[i] with { Dato = new(2030, 12, 5) };
            rader.RemoveAll(r => r.Tittel == "Sletting " + fase);
            await import.ImporterAsync(new() { Frister = rader });
        }
        await Publiser("før avmelding"); await p.Abonnement.MeldAvMedLenkeAsync(token);
        await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        await Publiser("under venting"); await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true, nye, endringer,
            alle ? null : [2031], alle ? null : [Kategori.Budsjett]);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        await p.Jobb().KjorAsync(); Assert.Single(p.Post.Hent("a"));
        await Publiser("etter aktivering");
        rader.Add(Importrad("Annet utvalg etter aktivering") with { Budsjettaar = 2040, Kategori = Kategori.Regnskap });
        await import.ImporterAsync(new() { Frister = rader });
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Jobb().KjorAsync();
        Assert.Equal(2, p.Post.Hent("a").Count); var melding = p.Post.Hent("a")[1];
        Assert.Equal(nye, melding.Tekst.Contains("Ny etter aktivering", StringComparison.Ordinal));
        Assert.Equal(endringer, melding.Tekst.Contains("Endring etter aktivering", StringComparison.Ordinal));
        Assert.Equal(endringer, melding.Tekst.Contains("Sletting etter aktivering", StringComparison.Ordinal));
        Assert.Equal(alle && nye, melding.Tekst.Contains("Annet utvalg etter aktivering", StringComparison.Ordinal));
        Assert.DoesNotContain("før avmelding", EposttekstBevis.UtenHemmelighet(melding.Tekst)); Assert.DoesNotContain("under venting", EposttekstBevis.UtenHemmelighet(melding.Tekst));
    }

    [Fact]
    public async Task Legacy_aktiv_uten_aktiveringsrad_har_grense_null_og_gammel_lenke_taper_for_ny_bekreftelse()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await using (var oppsett = db.Fabrikk().CreateDbContext())
            await oppsett.Database.ExecuteSqlRawAsync("DELETE FROM Epostaktiveringer WHERE BrukerId = 'a'");
        await p.Skriv.OpprettAsync(Inndata("Legacy-post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a")));
        await p.Abonnement.MeldAvMedLenkeAsync(token); Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
        p.Klokke.Flytt(TimeSpan.FromMinutes(2)); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        var aktiv = await p.Abonnement.HentAsync("a"); await p.Abonnement.MeldAvMedLenkeAsync(token);
        Assert.Equal(aktiv, await p.Abonnement.HentAsync("a"));
    }

    [Fact]
    public async Task Faktisk_lenke_avmelder_bare_eget_abonnement_og_gjenbruk_endrer_ingen_andre_data()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db);
        await p.Aktiver(); await p.Aktiver("b");
        await VelgUtvalg(p, [2031], [Kategori.Budsjett], true, true);
        await p.Skriv.OpprettAsync(Inndata("Egen og andres post")); await p.Jobb().KjorAsync();
        var a = await p.Abonnement.HentAsync("a"); var b = await p.Abonnement.HentAsync("b");
        var melding = Assert.Single(p.Post.Hent("a")); var token = Avmeldingstoken(melding);
        Assert.True(token.Length == 64 && melding.Html.Contains("/epostvarsling/avmeld#" + token, StringComparison.Ordinal));
        Assert.False(melding.ToString()!.Contains(token, StringComparison.Ordinal));
        Assert.False(EposttekstBevis.UtenHemmelighet(melding.Tekst).Contains(token, StringComparison.Ordinal));
        Assert.False(EposttekstBevis.UtenHemmelighet(melding.Html).Contains(token, StringComparison.Ordinal));
        var feed = new Aarshjul.Infrastructure.Kalender.KalenderabonnementTjeneste(db.Fabrikk(), p.Klokke);
        Assert.Null(await feed.HentAktivtUtvalgAsync(token));
        await p.Abonnement.MeldPaaAsync("venter", "venter@fin.dep.no", true);
        await Assert.ThrowsAsync<Aarshjul.Application.Valideringsfeil>(() => p.Abonnement.BekreftAsync("venter", token));
        Assert.Equal("VenterPaaBekreftelse", (await p.Abonnement.HentAsync("venter")).Status);
        await p.Abonnement.MeldAvMedLenkeAsync(token);
        var etter = await p.Abonnement.HentAsync("a");
        Assert.Equal("Avmeldt", etter.Status); Assert.Equal(a.Adresse, etter.Adresse);
        Assert.Equal(a.NyeFrister, etter.NyeFrister); Assert.Equal(a.VesentligeEndringer, etter.VesentligeEndringer);
        Assert.Equal(a.Budsjettaar, etter.Budsjettaar); Assert.Equal(a.Kategorier, etter.Kategorier);
        Assert.Equal(a.Aktivert, etter.Aktivert); Assert.Equal(b, await p.Abonnement.HentAsync("b"));
        await p.Abonnement.MeldAvMedLenkeAsync(token); Assert.True(System.Text.Json.JsonSerializer.Serialize(etter) == System.Text.Json.JsonSerializer.Serialize(await p.Abonnement.HentAsync("a")));
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Skriv.OpprettAsync(Inndata("Bare B videre")); await p.Jobb().KjorAsync();
        Assert.Single(p.Post.Hent("a")); Assert.Equal(2, p.Post.Hent("b").Count);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("feil")] [InlineData("ikke-hex")]
    [InlineData("ukjent")]
    public async Task Ugyldig_eller_manglende_lenke_gir_ingen_endring(string? token)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        var foer = await p.Abonnement.HentAsync("a");
        if (token == "ukjent") token = new string('A', 64);
        await p.Abonnement.MeldAvMedLenkeAsync(token); await p.Abonnement.MeldAvMedLenkeAsync(new string('A', 4097));
        Assert.Equal(foer, await p.Abonnement.HentAsync("a"));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task Tidligere_dagers_lenke_virker_etter_filterendring_senere_post_og_ny_tjeneste(int ticks)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første dag")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a")));
        await VelgUtvalg(p, [2031], [Kategori.Budsjett], true, true);
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Skriv.OpprettAsync(Inndata("Neste dag")); await p.Jobb().KjorAsync();
        Assert.Equal(2, p.Post.Hent("a").Count);
        Assert.False(token == Avmeldingstoken(p.Post.Hent("a")[1]));
        p.Klokke.Flytt(TimeSpan.FromTicks(ticks));
        IEpostabonnement ny = new EpostabonnementTjeneste(db.Fabrikk(), p.Klokke, p.Bekreftelsespost);
        await ny.MeldAvMedLenkeAsync(token); Assert.Equal("Avmeldt", (await ny.HentAsync("a")).Status);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, -1)] [InlineData(true, 0)] [InlineData(true, -1)]
    public async Task Gammel_lenke_stanser_ikke_ventende_eller_ny_livssyklus_ved_adressebytte_eller_gjenpaamelding(bool adresse, int ticks)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Gammel post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); var foersteAktivering = p.Klokke.GetUtcNow(); p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        if (adresse)
        {
            var status = await p.Abonnement.HentAsync("a");
            await p.Abonnement.EndreAdresseAsync("a", "ny@jd.dep.no", true, status.Versjon);
        }
        else
        {
            await p.Abonnement.MeldAvMedLenkeAsync(token); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
        }
        var venter = await p.Abonnement.HentAsync("a"); await p.Abonnement.MeldAvMedLenkeAsync(token);
        Assert.Equal(venter, await p.Abonnement.HentAsync("a"));
        await p.Skriv.OpprettAsync(Inndata("Inaktivt etterslep"));
        p.Klokke.Flytt(foersteAktivering - p.Klokke.GetUtcNow() + TimeSpan.FromTicks(ticks));
        await p.Abonnement.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
        var aktiv = await p.Abonnement.HentAsync("a"); await p.Abonnement.MeldAvMedLenkeAsync(token);
        Assert.Equal(aktiv, await p.Abonnement.HentAsync("a"));
        p.Klokke.Flytt(TimeSpan.FromDays(1)); await p.Skriv.OpprettAsync(Inndata("Bare etter ny aktivering")); await p.Jobb().KjorAsync();
        Assert.Equal(2, p.Post.Hent("a").Count); var nyPost = p.Post.Hent("a")[1];
        Assert.Contains("Bare etter ny aktivering", EposttekstBevis.UtenHemmelighet(nyPost.Tekst)); Assert.DoesNotContain("Inaktivt etterslep", EposttekstBevis.UtenHemmelighet(nyPost.Tekst));
        await p.Abonnement.MeldAvMedLenkeAsync(Avmeldingstoken(nyPost)); Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
    }

    [Fact]
    public async Task To_samtidige_POST_er_idempotente_paa_to_tjenesteinstanser()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Dobbelt avmelding")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a")));
        await Task.WhenAll(Task.Run(() => p.Abonnement.MeldAvMedLenkeAsync(token)), Task.Run(() => p.Abonnement.MeldAvMedLenkeAsync(token)));
        var status = await p.Abonnement.HentAsync("a"); Assert.Equal("Avmeldt", status.Status);
        await p.Abonnement.MeldAvMedLenkeAsync(token); Assert.Equal(status, await p.Abonnement.HentAsync("a"));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task Lenkeavmelding_ved_begge_sistekontroller_stanser_koelagt_oppsummering(int kontroll)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Tidligere post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Kølagt")); var brukt = false;
        var port = new FoerLagring(async kontekst =>
        {
            if (brukt) return;
            var koblinger = kontekst.ChangeTracker.Entries<Oppsummeringshendelse>().Any(x => x.State == EntityState.Added);
            var sending = kontekst.ChangeTracker.Entries<Epostoppsummering>().Any(x => x.State == EntityState.Modified && x.Entity.Status == "UnderSending");
            if (kontroll == 1 ? !sending || koblinger : !koblinger) return;
            brukt = true; await p.Abonnement.MeldAvMedLenkeAsync(token);
        });
        await p.Jobb(port).KjorAsync(); Assert.True(brukt); Assert.Single(p.Post.Hent("a"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Lenkeavmelding_bevarer_overlevert_UnderSending_og_Uavklart_uten_blind_retry(bool uavklart)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Overlevert")); var transport = new VentendePost(uavklart);
        var sending = p.Jobb(post: transport).KjorAsync();
        try
        {
            await transport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await p.Abonnement.MeldAvMedLenkeAsync(token);
            Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            await using (var les = db.Fabrikk().CreateDbContext())
            {
                var plan = await les.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2));
                Assert.Equal("UnderSending", plan.Status); Assert.True(plan.AvmeldingsHash?.Length == 64);
                Assert.Single(await les.Oppsummeringshendelser.Where(h => h.OppsummeringId == plan.Id).ToListAsync());
            }
            transport.Fortsett.TrySetResult(); await sending;
            await p.Jobb(post: transport).KjorAsync(); Assert.Equal(1, transport.Antall);
            await using var slutt = db.Fabrikk().CreateDbContext();
            Assert.Equal(uavklart ? "Uavklart" : "Bekreftet", (await slutt.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2))).Status);
        }
        finally { transport.Fortsett.TrySetResult(); await sending; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Avmelding_som_vinner_foer_overleveringslaasen_stanser_siste_gap_paa_to_instanser(bool fraLenke)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Kølagt siste gap"));
        var port = new FoerOverleveringslaas();
        var jobb = new DagligEpostoppsummering(db.FabrikkMedPort(port), p.Klokke, p.Tilgang, p.Post, Options.Create(new DagligEpostOpsjoner()));
        var sending = jobb.KjorAsync();
        try
        {
            await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (fraLenke) await p.Abonnement.MeldAvMedLenkeAsync(token); else await p.Abonnement.MeldAvAsync("a");
            port.Fortsett.TrySetResult(); await sending;
            Assert.Single(p.Post.Hent("a")); Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            await using var les = db.Fabrikk().CreateDbContext();
            Assert.Equal("Planlagt", (await les.Epostoppsummeringer.SingleAsync(x => x.LokalDato == new DateOnly(2030, 1, 2))).Status);
        }
        finally { port.Fortsett.TrySetResult(); await sending; }
    }

    [Fact]
    public async Task Siste_abonnementslesing_og_synkront_transportmottak_holder_samme_databaselaas()
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Første post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromDays(1));
        await p.Skriv.OpprettAsync(Inndata("Overleveringsgrensen"));
        var port = new SisteLesingPort();
        var transport = new Grensepost(port);
        var jobb = new DagligEpostoppsummering(db.FabrikkMedKommando(port), p.Klokke, p.Tilgang, transport, Options.Create(new DagligEpostOpsjoner()));
        var sending = jobb.KjorAsync();
        try
        {
            // Porten venter etter den siste SELECT-en mens skrivelåsen faktisk holdes.
            await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var avmeldingsport = new FoerOverleveringslaas(stans: false);
            IEpostabonnement annenInstans = new EpostabonnementTjeneste(db.FabrikkMedPort(avmeldingsport), p.Klokke, p.Bekreftelsespost);
            var avmelding = Task.Run(async () =>
            {
                await annenInstans.MeldAvMedLenkeAsync(token);
                port.Rekkefolge.Enqueue("Avmeldt");
            });
            await avmeldingsport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(avmelding.IsCompleted);
            port.Fortsett.TrySetResult();
            await transport.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await avmelding.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(new[] { "Overlevert", "Avmeldt" }, port.Rekkefolge.ToArray());
            Assert.Equal("Avmeldt", (await p.Abonnement.HentAsync("a")).Status);
            transport.Fortsett.TrySetResult(); await sending;
        }
        finally { port.Fortsett.TrySetResult(); transport.Fortsett.TrySetResult(); await sending; }
    }

    [Theory]
    [InlineData("valg", false)] [InlineData("valg", true)]
    [InlineData("adresse", false)] [InlineData("adresse", true)]
    [InlineData("bekreftelse", false)] [InlineData("bekreftelse", true)]
    [InlineData("gjenpaamelding", false)] [InlineData("gjenpaamelding", true)]
    public async Task Lenkeavmelding_og_samtidig_eierhandling_respekterer_begge_vinnerrekkefolger(string handling, bool lenkeFoerst)
    {
        await using var db = await OpprettAsync(); var p = await Proeve.Opprett(db); await p.Aktiver();
        await p.Skriv.OpprettAsync(Inndata("Gammel post")); await p.Jobb().KjorAsync();
        var token = Avmeldingstoken(Assert.Single(p.Post.Hent("a"))); p.Klokke.Flytt(TimeSpan.FromMinutes(2));
        if (handling is "bekreftelse" or "gjenpaamelding")
        {
            if (handling == "bekreftelse")
            {
                var aktiv = await p.Abonnement.HentAsync("a");
                await p.Abonnement.EndreAdresseAsync("a", "ny@jd.dep.no", true, aktiv.Versjon);
            }
            else
            {
                await p.Abonnement.MeldAvAsync("a"); await p.Abonnement.MeldPaaAsync("a", "egen@fin.dep.no", true);
            }
        }
        var foer = await p.Abonnement.HentAsync("a");
        async Task Eierhandling(IEpostabonnement tjeneste)
        {
            if (handling is "bekreftelse" or "gjenpaamelding")
                await tjeneste.BekreftAsync("a", p.Bekreftelsespost.Hent("a")!.Token);
            else if (handling == "adresse")
                await tjeneste.EndreAdresseAsync("a", "ny@jd.dep.no", true, foer.Versjon);
            else await tjeneste.EndreValgAsync("a", true, true, foer.Versjon, [2031], [Kategori.Budsjett]);
        }
        if (lenkeFoerst)
        {
            var port = new FoerLagring(async _ => await p.Abonnement.MeldAvMedLenkeAsync(token));
            IEpostabonnement forsinket = new EpostabonnementTjeneste(db.Fabrikk(port), p.Klokke, p.Bekreftelsespost);
            if (handling is "valg" or "adresse")
                await Assert.ThrowsAsync<EpostabonnementKonflikt>(() => Eierhandling(forsinket));
            else await Eierhandling(forsinket);
        }
        else
        {
            var port = new FoerOverleveringslaas();
            IEpostabonnement forsinket = new EpostabonnementTjeneste(db.FabrikkMedPort(port), p.Klokke, p.Bekreftelsespost);
            var avmelding = forsinket.MeldAvMedLenkeAsync(token);
            try
            {
                await port.Ankommet.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await Eierhandling(p.Abonnement); port.Fortsett.TrySetResult(); await avmelding;
            }
            finally { port.Fortsett.TrySetResult(); await avmelding; }
        }
        var etter = await p.Abonnement.HentAsync("a");
        Assert.Equal(handling is "bekreftelse" or "gjenpaamelding" ? "Aktiv" : handling == "adresse" && !lenkeFoerst ? "VenterPaaBekreftelse" : "Avmeldt", etter.Status);
        if (handling is "bekreftelse" or "gjenpaamelding")
        {
            await p.Abonnement.MeldAvMedLenkeAsync(token); Assert.Equal(etter, await p.Abonnement.HentAsync("a"));
        }
    }

    private sealed class FoerOverleveringslaas(bool stans = true) : DbTransactionInterceptor
    {
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection tilkobling, TransactionStartingEventData data,
            InterceptionResult<DbTransaction> result, CancellationToken ct = default)
        {
            var planer = data.Context!.ChangeTracker.Entries<Epostoppsummering>().ToList();
            if (planer.Count == 0 || planer.Any(p => p.Entity.Status == "UnderSending" && p.Entity.AvmeldingsHash != null && p.State == EntityState.Unchanged))
            {
                Ankommet.TrySetResult();
                if (stans) await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }

    private sealed class SisteLesingPort : DbCommandInterceptor
    {
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ConcurrentQueue<string> Rekkefolge = new();
        public bool Laast;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand kommando, CommandExecutedEventData data,
            DbDataReader result, CancellationToken ct = default)
        {
            if (kommando.CommandText.Contains("Epostabonnementer", StringComparison.Ordinal) && kommando.Transaction is not null)
            {
                Laast = true;
                Ankommet.TrySetResult(); await Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            }
            return result;
        }
    }

    private sealed class Grensepost(SisteLesingPort port) : IOppsummeringspost
    {
        public bool Tilgjengelig => true;
        public readonly TaskCompletionSource Ankommet = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Fortsett = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Overleveringskvittering StartOverlevering(Oppsummeringsmelding melding, CancellationToken ct = default)
        {
            Assert.True(port.Laast, "Transportmottaket krever siste lesing under databasekoordinering.");
            port.Rekkefolge.Enqueue("Overlevert"); Ankommet.TrySetResult();
            return new(Fortsett.Task.WaitAsync(TimeSpan.FromSeconds(15), ct));
        }
    }

    private static string Avmeldingstoken(Oppsummeringsmelding melding)
    {
        var treff = Regex.Match(melding.Tekst, @"/epostvarsling/avmeld#([A-F0-9]{64})(?:\s|$)");
        Assert.True(treff.Success, "Oppsummeringen må ha en avmeldingslenke.");
        return treff.Groups[1].Value;
    }
}

/// <summary>Bevar innholdsassertions uten at xUnit gjengir meldingen med en hemmelig lenke ved feil.</summary>
public static class EposttekstBevis
{
    public static string UtenHemmelighet(string tekst)
        => Regex.Replace(tekst, @"https?://[^\s""'<>]*/epostvarsling/avmeld#[A-Fa-f0-9]{64}", "[skjult avmeldingslenke]");
}
