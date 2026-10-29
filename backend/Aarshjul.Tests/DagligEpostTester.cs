using Aarshjul.Infrastructure;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Testkontrakter;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Aarshjul.Tests;

public sealed class DagligEpostTester : DagligEpostKontrakt
{
    [Fact]
    public async Task EV09_demooppgradering_fra_levert_main140_bevarer_kildelenker_og_fullfoerer_med_snever_avmeldingsrett()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettLevertMainSkjemaAsync();
        var bekreftelsespost = await EpostoppgraderingBevis.LeggTilAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV08PlanAsync(database);
        await using (var oppsett = database.Fabrikk().CreateDbContext())
            await oppsett.Database.ExecuteSqlRawAsync("UPDATE Frister SET ManuellKildeUrl = 'https://regjeringen.no/no/levert-main140'");
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varsling = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            Assert.All(await db.Frister.ToListAsync(), f => Assert.Equal("https://regjeringen.no/no/levert-main140", f.ManuellKildeUrl));
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varsling, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV09PlanAsync(database, plan, bekreftelsespost, avmeld: true);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Demooppgradering_fra_hver_levert_gren_bevarer_lenke_eller_historisk_synlighet_og_ventende_plan(bool manuellFoerst)
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV05SkjemaAsync();
        await EpostoppgraderingBevis.LeggTilAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV04PlanAsync(database);
        await EpostoppgraderingBevis.InstallerGrenmigrasjonAsync(database, manuellFoerst
            ? new Aarshjul.Infrastructure.Migrations.ManuellKildelenke()
            : new Aarshjul.Infrastructure.Migrations.HistoriskSlettingssynlighet());
        await using (var oppsett = database.Fabrikk().CreateDbContext())
        {
            if (manuellFoerst)
                await oppsett.Database.ExecuteSqlRawAsync("UPDATE Frister SET ManuellKildeUrl = 'https://regjeringen.no/no/bevart-gren140'");
            else
            {
                oppsett.Set<FristHendelsessynlighet>().Add(new() { HendelseId = plan.Behandlet, GruppeKode = "FAG" });
                await oppsett.SaveChangesAsync();
            }
        }
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varsling = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            Assert.All(await db.Frister.ToListAsync(), f => Assert.Equal(manuellFoerst ? "https://regjeringen.no/no/bevart-gren140" : null, f.ManuellKildeUrl));
            Assert.Equal(manuellFoerst ? 0 : 1, await db.Set<FristHendelsessynlighet>().CountAsync());
            Assert.Equal(await db.Database.SqlQueryRaw<long>("SELECT Verdi AS Value FROM Epostrekkefolge WHERE Id = 1").SingleAsync(),
                await db.Database.SqlQueryRaw<long>("SELECT Livssyklussekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync());
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varsling, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV05PlanAsync(database, plan);
    }

    [Fact]
    public async Task EV05_demo_oppgraderes_idempotent_med_nullable_manuell_lenke_uten_tilbakefylling()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV05SkjemaAsync();
        var id = Guid.NewGuid();
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Frister (Id, Tittel, Dato, Datopresisjon, Sorteringsdag, Budsjettaar, Kategori, Status, Opphav) VALUES ({id}, 'Bevart EV05-frist', '2030-11-20', 0, '2030-11-20', 2031, 0, 0, 0)");
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            var frist = Assert.Single(await db.Frister.ToListAsync());
            Assert.Equal(id, frist.Id); Assert.Equal("Bevart EV05-frist", frist.Tittel);
            Assert.Null(frist.ManuellKildeUrl); Assert.Empty(await db.NyFristHendelser.ToListAsync());
            frist.ManuellKildeUrl = "https://regjeringen.no/no/demo140"; await db.SaveChangesAsync();
        }
        await using var etter = database.Fabrikk().CreateDbContext();
        await Demoskjema.SikreAsync(etter);
        Assert.Equal("https://regjeringen.no/no/demo140", (await etter.Frister.SingleAsync()).ManuellKildeUrl);
    }
    [Fact]
    public async Task EV09_demooppgradering_fra_EV08_bevarer_data_og_ventende_plan_og_fullfoerer_med_avmeldingslenke()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV08SkjemaAsync();
        var bekreftelsespost = await EpostoppgraderingBevis.LeggTilAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV08PlanAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varsling = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            var kolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostoppsummeringer')").ToListAsync();
            Assert.Contains("AvmeldingsHash", kolonner);
            Assert.Contains("Avmeldingssekvens", kolonner);
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varsling, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV09PlanAsync(database, plan, bekreftelsespost);
    }
    [Fact]
    public async Task EV07_demooppgradering_fra_EV05_bevarer_data_og_planer_uten_historisk_tilbakefylling()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV05SkjemaAsync();
        await EpostoppgraderingBevis.LeggTilAsync(database);
        const string utvalg = """{"Budsjettaar":null,"Kategorier":null,"Grenser":[{"Budsjettaar":null,"Kategorier":null,"Sekvens":0}]}""";
        await using (var oppsett = database.Fabrikk().CreateDbContext())
            await oppsett.Database.ExecuteSqlInterpolatedAsync($"UPDATE Epostabonnementer SET UtvalgJson = {utvalg} WHERE BrukerId = 'eldre'");
        var plan = await EpostoppgraderingBevis.LeggTilEV04PlanAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varsling = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            Assert.Empty(await db.Set<FristHendelsessynlighet>().ToListAsync());
            Assert.Equal(utvalg, await db.Epostabonnementer.Where(a => a.BrukerId == "eldre").Select(a => a.UtvalgJson).SingleAsync());
            Assert.Equal(await db.Database.SqlQueryRaw<long>("SELECT Verdi AS Value FROM Epostrekkefolge WHERE Id = 1").SingleAsync(),
                await db.Database.SqlQueryRaw<long>("SELECT Livssyklussekvens AS Value FROM Epostaktiveringer WHERE BrukerId = 'eldre'").SingleAsync());
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varsling, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV05PlanAsync(database, plan);
    }
    [Fact]
    public async Task EV05_demooppgradering_bevarer_EV04_og_fullfoerer_ventende_plan_med_legacy_metadata()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV04SkjemaAsync();
        await EpostoppgraderingBevis.LeggTilAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV04PlanAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varslingFoer = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
            Assert.All(await db.Epostabonnementer.ToListAsync(), a => Assert.Null(a.UtvalgJson));
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varslingFoer, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV05PlanAsync(database, plan);
    }
    [Fact]
    public async Task EV04_demooppgradering_bevarer_EV03_hendelser_planer_koblinger_og_sekvensgrenser()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV03SkjemaAsync();
        await EpostoppgraderingBevis.LeggTilAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV03PlanAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        { await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db); }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        await EpostoppgraderingBevis.BekreftEV04PlanAsync(database, plan);
    }
    [Fact]
    public async Task Additiv_EV03_migrasjon_bevarer_EV02_frister_kilder_forslag_synlighet_feed_og_internkalender()
    {
        await using var database = new Sqlitedatabase();
        await database.OpprettTidligereSkjemaAsync();
        var bekreftelsespost = await EpostoppgraderingBevis.LeggTilAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await Demoskjema.SikreAsync(db);
            await Demoskjema.SikreAsync(db); // Den samme normale oppstartsgrensen er idempotent.
        }
        Assert.True(foer == await EpostoppgraderingBevis.HentAsync(database), "EV02-data må bevares gjennom additiv EV03-oppgradering.");
        await EpostoppgraderingBevis.BekreftInternvern(database);
        await EpostoppgraderingBevis.BekreftIngenTilbakefylling(database, bekreftelsespost);
    }
    protected override async Task<Epostdatabase> OpprettAsync()
    {
        var database = new Sqlitedatabase();
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }
    private sealed class Sqlitedatabase : Epostdatabase
    {
        private readonly string _tilkobling = $"Data Source=epost-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _holder;
        public Sqlitedatabase() { _holder = new(_tilkobling); _holder.Open(); }
        public async Task OpprettTidligereSkjemaAsync()
        {
            await using var db = new TidligereKontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        public async Task OpprettEV03SkjemaAsync()
        {
            await using var db = new EV03Kontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        public async Task OpprettEV04SkjemaAsync()
        {
            await using var db = new EV04Kontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        public async Task OpprettEV05SkjemaAsync()
        {
            await using var db = new EV05Kontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        public async Task OpprettEV08SkjemaAsync()
        {
            await using var db = new EV08Kontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        public async Task OpprettLevertMainSkjemaAsync()
        {
            await using var db = new LevertMainKontekst(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling).Options);
            await db.Database.EnsureCreatedAsync();
        }
        protected override DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling);
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
        }
        public override ValueTask DisposeAsync() => _holder.DisposeAsync();
    }
    private sealed class TidligereKontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl);
            EpostoppgraderingBevis.IgnorerEV09(b);
            b.Entity<Epostabonnement>().Ignore(x => x.UtvalgJson);
            b.Ignore<NyFristHendelse>();
            b.Ignore<FristHendelsessynlighet>();
            b.Ignore<Epostoppsummering>();
            b.Ignore<Oppsummeringshendelse>();
            b.Ignore("Aarshjul.Infrastructure.Epost.Epostrekkefolge");
            b.Ignore("Aarshjul.Infrastructure.Epost.Epostaktivering");
        }
    }
    private sealed class EV03Kontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);
            b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl);
            EpostoppgraderingBevis.IgnorerEV09(b);
            b.Entity<Epostabonnement>().Ignore(x => x.UtvalgJson);
            var hendelse = b.Entity<NyFristHendelse>();
            b.Ignore<FristHendelsessynlighet>();
            b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Livssyklussekvens");
            hendelse.Ignore(x => x.Type); hendelse.Ignore(x => x.FoerJson); hendelse.Ignore(x => x.EtterJson); hendelse.Ignore(x => x.Forklaring);
            hendelse.HasIndex(x => x.FristId).IsUnique().HasFilter(null);
            b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Endringssekvens");
        }
    }
    private sealed class EV04Kontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        { base.OnModelCreating(b); b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl); b.Entity<Epostabonnement>().Ignore(x => x.UtvalgJson);
            EpostoppgraderingBevis.IgnorerEV09(b);
            b.Ignore<FristHendelsessynlighet>(); b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Livssyklussekvens"); }
    }
    private sealed class EV05Kontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b); b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl); b.Ignore<FristHendelsessynlighet>();
            EpostoppgraderingBevis.IgnorerEV09(b);
            b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Livssyklussekvens");
        }
    }
    private sealed class LevertMainKontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        { base.OnModelCreating(b); EpostoppgraderingBevis.IgnorerEV09(b); }
    }
    private sealed class EV08Kontekst(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder b)
        { base.OnModelCreating(b); b.Entity<Frist>().Ignore(x => x.ManuellKildeUrl); EpostoppgraderingBevis.IgnorerEV09(b); }
    }
}
