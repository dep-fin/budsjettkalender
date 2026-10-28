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

public sealed class DagligEpostTester : DagligEpostKontrakt
{
    [Fact]
    public async Task EV04_migrasjon_fra_levert_EV03_bevarer_hendelser_planer_koblinger_og_sekvensgrenser()
    {
        await using var database = new Sqldatabase(); await database.OpprettAsync("20261028142358_OrdnetEpostaktivering");
        await EpostoppgraderingBevis.LeggTilAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV03PlanAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.MigrateAsync();
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        await EpostoppgraderingBevis.BekreftEV04PlanAsync(database, plan);
    }
    [Fact]
    public async Task Additiv_EV03_migrasjon_bevarer_EV02_og_hele_datagrunnlaget_uten_varslingsflom()
    {
        await using var database = new Sqldatabase();
        await database.OpprettAsync("20261028083448_Epostabonnement");
        var bekreftelsespost = await EpostoppgraderingBevis.LeggTilAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.MigrateAsync();
        Assert.True(foer == await EpostoppgraderingBevis.HentAsync(database), "EV02-data må bevares gjennom additiv EV03-oppgradering.");
        await EpostoppgraderingBevis.BekreftInternvern(database);
        await EpostoppgraderingBevis.BekreftIngenTilbakefylling(database, bekreftelsespost);
    }

    [Fact]
    public async Task Oppgradering_fra_levert_UX16_bevarer_internvern_aktive_og_ventende_abonnementer_uten_varslingsflom()
    {
        await using var database = new Sqldatabase();
        await database.OpprettAsync("20261028112544_UnikeInternrunder");
        var bekreftelsespost = await EpostoppgraderingBevis.LeggTilAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Assert.Equal(new[] { "20261028133321_DagligEpostoppsummering", "20261028142358_OrdnetEpostaktivering", "20261028165341_VarseltyperOgEndringer" },
                (await db.Database.GetAppliedMigrationsAsync()).TakeLast(3));
        }
        Assert.True(foer == await EpostoppgraderingBevis.HentAsync(database), "Leverte UX16-/EV02-data må bevares gjennom EV03-oppgraderingen.");
        await EpostoppgraderingBevis.BekreftInternvern(database);
        await EpostoppgraderingBevis.BekreftIngenTilbakefylling(database, bekreftelsespost);
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
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_tilkobling, sql => sql.MaxBatchSize(1));
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
