using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Testkontrakter;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Aarshjul.SqlServerTests;

public sealed class EpostabonnementTester : EpostabonnementKontrakt
{
    [Fact]
    public async Task Additiv_migrasjon_bevarer_tidligere_data_uten_automatisk_paamelding()
    {
        await using var database = new Sqldatabase();
        await database.OpprettAsync("20261027205350_FellesFasemal");
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var foer = await FasedataBevis.HentAsync(database.Fabrikk());
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.MigrateAsync();
        Assert.Equal(foer, await FasedataBevis.HentAsync(database.Fabrikk()));
        var post = new KontrollertBekreftelsespost();
        var tjeneste = new EpostabonnementTjeneste(database.Fabrikk(), TimeProvider.System, post);
        Assert.Equal("IkkePaameldt", (await tjeneste.HentAsync("eksisterende-bruker")).Status);
        Assert.Null(post.Hent("eksisterende-bruker"));
        await tjeneste.MeldPaaAsync("eksisterende-bruker", "ev02@fin.dep.no", true);
        await tjeneste.BekreftAsync("eksisterende-bruker", post.Hent("eksisterende-bruker")!.Token);
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.MigrateAsync();
        Assert.Equal("Aktiv", (await new EpostabonnementTjeneste(database.Fabrikk(), TimeProvider.System, post).HentAsync("eksisterende-bruker")).Status);
        Assert.Equal(foer, await FasedataBevis.HentAsync(database.Fabrikk()));
    }

    protected override async Task<Epostdatabase> OpprettAsync()
    {
        var database = new Sqldatabase();
        try { await database.OpprettAsync(); return database; }
        catch { await database.DisposeAsync(); throw; }
    }

    private sealed class Sqldatabase : Epostdatabase
    {
        private readonly string _navn = $"AarshjulEpost_{Guid.NewGuid():N}";
        private readonly string _master, _tilkobling;
        private bool _opprettet;
        public Sqldatabase()
        {
            var verdi = Environment.GetEnvironmentVariable("AARSHJUL_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER må peke på master på en disponibel SQL Server. Ingen SQL-tester hoppes over.");
            SqlConnectionStringBuilder builder;
            try { builder = new(verdi); }
            catch (ArgumentException) { throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER er ikke en gyldig SQL Server-tilkobling."); }
            if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) || builder.AttachDBFilename.Length > 0)
                throw new InvalidOperationException("SQL-testene krever Database=master og tillater ikke en eksisterende applikasjonsdatabase eller databasefil.");
            builder.ConnectTimeout = 5; builder.Pooling = false;
            _master = builder.ConnectionString;
            builder.InitialCatalog = _navn; _tilkobling = builder.ConnectionString;
        }
        public async Task OpprettAsync(string? migrasjon = null)
        {
            using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                while (true)
                {
                    try { await KjorMasterAsync("SELECT 1;", frist.Token); break; }
                    catch (SqlException) when (!frist.IsCancellationRequested) { await Task.Delay(1000, frist.Token); }
                }
            }
            catch (Exception feil) when (feil is SqlException or OperationCanceledException)
            { throw new InvalidOperationException("SQL Server svarte ikke på SELECT 1 innen 60 sekunder."); }
            await KjorMasterAsync($"CREATE DATABASE [{_navn}];"); _opprettet = true;
            await using var db = Fabrikk().CreateDbContext();
            if (migrasjon is null) await db.Database.MigrateAsync();
            else await db.GetService<IMigrator>().MigrateAsync(migrasjon);
        }
        protected override DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_tilkobling);
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
        }
        private async Task KjorMasterAsync(string sql, CancellationToken ct = default)
        {
            await using var forbindelse = new SqlConnection(_master);
            await forbindelse.OpenAsync(ct);
            await using var kommando = forbindelse.CreateCommand(); kommando.CommandText = sql;
            await kommando.ExecuteNonQueryAsync(ct);
        }
        public override async ValueTask DisposeAsync()
        {
            if (!_opprettet) return;
            await KjorMasterAsync($"ALTER DATABASE [{_navn}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_navn}];");
            _opprettet = false;
        }
    }
}
