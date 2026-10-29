using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Godkjenningsko;
using Aarshjul.Testkontrakter;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Aarshjul.SqlServerTests;

public sealed class GodkjenningskonkurranseTester : GodkjenningskonkurranseKontrakt
{
    [Fact]
    public async Task Migrering_bevarer_og_tillater_behandling_av_eksisterende_forslag()
    {
        await using var database = new SqlServerGodkjenningsdatabase();
        var forslagId = Guid.NewGuid();
        await database.OpprettAsync(forslagId);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = "FA", Navn = "FA" });
            await db.SaveChangesAsync();
        }

        IGodkjenningsko administrator = new GodkjenningskoTjeneste(database.Fabrikk());
        var forslag = Assert.Single(await administrator.HentKoAsync());
        Assert.Equal(forslagId, forslag.Id);
        Assert.Equal("Eksisterende forslag", forslag.Tittel);
        Assert.Equal(new DateOnly(2027, 3, 15), forslag.Dato);
        var fristId = await administrator.GodkjennAsync(new GodkjennInndata { ForslagId = forslagId, Synlighetskoder = ["FA"] });
        Assert.Empty(await administrator.HentKoAsync());
        IFristDatautveksling eksport = new FristDatautveksling(database.Fabrikk(), TimeProvider.System);
        var frist = Assert.Single((await eksport.EksporterAsync()).Frister);
        Assert.Equal(fristId, frist.Id);
        Assert.Equal("Eksisterende forslag", frist.Tittel);
        Assert.Equal(new DateOnly(2027, 3, 15), frist.Dato);
    }

    protected override async Task<Godkjenningsdatabase> OpprettDatabaseAsync()
    {
        var database = new SqlServerGodkjenningsdatabase();
        try
        {
            await database.OpprettAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private sealed class SqlServerGodkjenningsdatabase : Godkjenningsdatabase
    {
        // Navnet kommer aldri fra miljøvariabler. Bare databasen dette objektet oppretter slettes.
        private readonly string _databasenavn = $"AarshjulGodkjenning_{Guid.NewGuid():N}";
        private readonly string _mastertilkobling;
        private readonly string _testtilkobling;
        private bool _opprettet;

        public SqlServerGodkjenningsdatabase()
        {
            var tilkobling = Environment.GetEnvironmentVariable("AARSHJUL_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER må peke på master på en disponibel SQL Server. SQL-testene kan ikke hoppes over.");
            SqlConnectionStringBuilder builder;
            try { builder = new SqlConnectionStringBuilder(tilkobling); }
            catch (ArgumentException) { throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER er ikke en gyldig SQL Server-tilkobling."); }
            if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) || builder.AttachDBFilename.Length > 0)
                throw new InvalidOperationException("SQL-testene krever Database=master og tillater ikke en eksisterende applikasjonsdatabase eller databasefil.");
            builder.ConnectTimeout = 5;
            builder.Pooling = false;
            _mastertilkobling = builder.ConnectionString;
            builder.InitialCatalog = _databasenavn;
            _testtilkobling = builder.ConnectionString;
        }

        public async Task OpprettAsync(Guid? eksisterendeForslagId = null)
        {
            await VentPaSqlServerAsync();
            await KjorMasterAsync($"CREATE DATABASE [{_databasenavn}];");
            _opprettet = true;
            await using var db = Fabrikk().CreateDbContext();
            if (eksisterendeForslagId is { } forslagId)
            {
                // Gjeldende produksjonsskjema har ingen Versjon-kolonne. Legg inn en rad før oppgraderingen.
                await db.GetService<IMigrator>().MigrateAsync("20260803115159_Internkalender");
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO dbo.Forslag
                        (Id, ForslagType, Opphav, Tittel, Dato, Datopresisjon, Budsjettaar, Kategori, ForeslaattSynlighet, Status)
                    VALUES ({forslagId}, 0, 0, N'Eksisterende forslag', '2027-03-15', 0, 2028, 0, N'[]', 0);
                    """);
            }
            // Bruk produksjonens migrasjonskjede, slik at også pending-model-feil stopper CI.
            await db.Database.MigrateAsync();
        }

        private async Task VentPaSqlServerAsync()
        {
            using var tidsgrense = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                while (true)
                {
                    try
                    {
                        await using var connection = new SqlConnection(_mastertilkobling);
                        await connection.OpenAsync(tidsgrense.Token);
                        await using var command = connection.CreateCommand();
                        command.CommandText = "SELECT 1;";
                        await command.ExecuteScalarAsync(tidsgrense.Token);
                        return;
                    }
                    catch (SqlException) when (!tidsgrense.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), tidsgrense.Token);
                    }
                }
            }
            catch (Exception ex) when (ex is SqlException or OperationCanceledException)
            {
                // Tilkoblingsstreng og legitimasjon skal heller ikke havne i feilloggen.
                throw new InvalidOperationException("SQL Server svarte ikke på SELECT 1 innen 60 sekunder. SQL-integrasjonstesten feiler.");
            }
        }

        protected override DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port)
        {
            // Flere faktiske kommandoer gjør rollback-testen uavhengig av en samlet SQL-batch.
            var builder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(_testtilkobling, sql => sql.MaxBatchSize(1));
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
        }

        public override async Task InstallerLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER dbo.Testfeil ON dbo.FristSynlighet AFTER INSERT AS
                BEGIN
                    SET NOCOUNT ON;
                    IF (SELECT COUNT(*) FROM dbo.Frister) = 1
                       AND (SELECT COUNT(*) FROM dbo.Varsler) = 1
                       AND (SELECT COUNT(*) FROM dbo.NyFristHendelser WHERE Type = 'Ny') = 1
                       AND (SELECT Verdi FROM dbo.Epostrekkefolge WHERE Id = 1) = 1
                       AND (SELECT COUNT(*) FROM dbo.Forslag WHERE Status = 1) = 1
                        THROW 51001, 'Testfeil etter publiseringsskriv', 1;
                    ELSE
                        THROW 51002, 'Testoppsett: publiseringsskriv mangler', 1;
                END;
                """);
        }

        public override async Task FjernLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER dbo.Testfeil;");
        }

        private async Task KjorMasterAsync(string sql)
        {
            await using var connection = new SqlConnection(_mastertilkobling);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_opprettet) return;
            await KjorMasterAsync($"ALTER DATABASE [{_databasenavn}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databasenavn}];");
            _opprettet = false;
        }
    }
}
