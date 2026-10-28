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
            b.Ignore<NyFristHendelse>();
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
            var hendelse = b.Entity<NyFristHendelse>();
            hendelse.Ignore(x => x.Type); hendelse.Ignore(x => x.FoerJson); hendelse.Ignore(x => x.EtterJson); hendelse.Ignore(x => x.Forklaring);
            hendelse.HasIndex(x => x.FristId).IsUnique().HasFilter(null);
            b.Entity("Aarshjul.Infrastructure.Epost.Epostaktivering").Ignore("Endringssekvens");
        }
    }
}
