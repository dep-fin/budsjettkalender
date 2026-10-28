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

public sealed class EpostabonnementTester : EpostabonnementKontrakt
{
    [Fact]
    public async Task Additive_migrasjonsoperasjoner_kan_brukes_paa_tidligere_SQLite_skjema_uten_datatap()
    {
        await using var database = new Sqlitedatabase();
        await database.OpprettTidligereSkjemaAsync();
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var foer = await FasedataBevis.HentAsync(database.Fabrikk());
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            // Demooppstart kjører fortsatt EnsureCreated. Her prøves bare de additive
            // migrasjonsoperasjonene med SQLite-providerens SQL, ikke automatisk demooppgradering.
            var migrasjon = new Aarshjul.Infrastructure.Migrations.Epostabonnement();
            var sql = db.GetService<IMigrationsSqlGenerator>().Generate(migrasjon.UpOperations, db.Model);
            foreach (var kommando in sql) await db.Database.ExecuteSqlRawAsync(kommando.CommandText);
            foreach (var kommando in db.GetService<IMigrationsSqlGenerator>().Generate(new Aarshjul.Infrastructure.Migrations.Abonnementsutvalg().UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(kommando.CommandText.Replace("nvarchar(max)", "TEXT", StringComparison.Ordinal));
        }
        Assert.Equal(foer, await FasedataBevis.HentAsync(database.Fabrikk()));
        var post = new KontrollertBekreftelsespost();
        var tjeneste = new EpostabonnementTjeneste(database.Fabrikk(), TimeProvider.System, post);
        Assert.Equal("IkkePaameldt", (await tjeneste.HentAsync("eldre-bruker")).Status);
        await tjeneste.MeldPaaAsync("eldre-bruker", "test@fin.dep.no", true);
        var token = post.Hent("eldre-bruker")!.Token;
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var lagret = await db.Epostabonnementer.SingleAsync();
            Assert.True(lagret.BekreftelsesHash is { Length: 64 } && lagret.BekreftelsesHash != token, "Databasen skal bare bevare et enveisavtrykk av bekreftelsen.");
        }
        Assert.Equal("Aktiv", (await tjeneste.BekreftAsync("eldre-bruker", token)).Status);
        Assert.Equal(foer, await FasedataBevis.HentAsync(database.Fabrikk()));
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
            b.Ignore<Epostabonnement>();
        }
    }
}
