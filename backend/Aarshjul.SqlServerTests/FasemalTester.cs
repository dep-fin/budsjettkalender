using Aarshjul.Application.Faser;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Faser;
using Aarshjul.Testkontrakter;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Aarshjul.SqlServerTests;

public sealed class FasemalTester : FasemalKontrakt
{
    [Fact]
    public async Task Oppgradering_bevarer_hele_eksisterende_datagrunnlaget_og_redigert_mal_ved_ny_oppstart()
    {
        await using var database = new SqlServerFasedatabase();
        await database.OpprettAsync("20261020135232_ForslagVersjonKonkurransekontroll");
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var historikk = await FasedataBevis.HentAsync(database.Fabrikk());
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            Assert.DoesNotContain("20261027205350_FellesFasemal", await db.Database.GetAppliedMigrationsAsync());
            await db.Database.MigrateAsync();
            Assert.Contains("20261027205350_FellesFasemal", await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(1, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.Fasemaler").SingleAsync());
        }
        Assert.Equal(historikk, await FasedataBevis.HentAsync(database.Fabrikk()));
        IFasemal tjeneste = new FasemalTjeneste(database.Fabrikk());
        var standard = await tjeneste.HentAsync();
        Assert.Equal(new[] { -15, -10, -3, 0, 12 }, standard.Faser.Select(f => f.Startmaaned));
        Assert.Equal(new[] { "Forberedelser", "Utarbeidelse av budsjettforslag", "Fremleggelse og vedtak", "Gjennomføring og revidering", "Regnskap" }, standard.Faser.Select(f => f.Navn));
        Assert.Equal(15, standard.SisteMaanedInkludert);
        var lagret = await tjeneste.OppdaterAsync(standard.Versjon, EndredeFaser(), 17);
        // Samme oppstartssekvens som produksjon, ny kontekst og samme database.
        await using (var nyOppstart = database.Fabrikk().CreateDbContext())
        {
            await nyOppstart.Database.MigrateAsync();
            await Startdata.SeedAsync(nyOppstart, new());
            Assert.Equal(1, await nyOppstart.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.Fasemaler").SingleAsync());
        }
        Like(lagret, await new FasemalTjeneste(database.Fabrikk()).HentAsync());
        Assert.Equal(historikk, await FasedataBevis.HentAsync(database.Fabrikk()));
    }

    protected override async Task<Fasedatabase> OpprettDatabaseAsync()
    {
        var database = new SqlServerFasedatabase();
        try { await database.OpprettAsync(); return database; }
        catch { await database.DisposeAsync(); throw; }
    }

    private sealed class SqlServerFasedatabase : Fasedatabase
    {
        private readonly string _navn = $"AarshjulFaser_{Guid.NewGuid():N}";
        private readonly string _master;
        private readonly string _tilkobling;
        private bool _opprettet;
        public SqlServerFasedatabase()
        {
            var tilkobling = Environment.GetEnvironmentVariable("AARSHJUL_TEST_SQLSERVER")
                ?? throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER må peke på master på en disponibel SQL Server. SQL-testene kan ikke hoppes over.");
            SqlConnectionStringBuilder builder;
            try { builder = new(tilkobling); }
            catch (ArgumentException) { throw new InvalidOperationException("AARSHJUL_TEST_SQLSERVER er ikke en gyldig SQL Server-tilkobling."); }
            if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) || builder.AttachDBFilename.Length > 0)
                throw new InvalidOperationException("SQL-testene krever Database=master og tillater ikke en eksisterende applikasjonsdatabase eller databasefil.");
            builder.ConnectTimeout = 5;
            builder.Pooling = false;
            _master = builder.ConnectionString;
            builder.InitialCatalog = _navn;
            _tilkobling = builder.ConnectionString;
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
            catch (Exception ex) when (ex is SqlException or OperationCanceledException)
            { throw new InvalidOperationException("SQL Server svarte ikke på SELECT 1 innen 60 sekunder. SQL-integrasjonstesten feiler."); }
            await KjorMasterAsync($"CREATE DATABASE [{_navn}];");
            _opprettet = true;
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
        public override async Task InstallerLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            // Ekte databasefeil under UPDATE, uten å endre EF-modellen eller produksjonsadapteren.
            // Testens nye navn avvises av databasen; den eksisterende standardmalen er gyldig.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Fasemaler ADD CONSTRAINT CK_Testfasefeil CHECK (Innhold NOT LIKE N'%Tidlig plan%');");
        }
        public override async Task FjernLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Fasemaler DROP CONSTRAINT CK_Testfasefeil;");
        }
        private async Task KjorMasterAsync(string sql, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_master);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }
        public override async ValueTask DisposeAsync()
        {
            if (!_opprettet) return;
            // Navnet er generert her; sletting kan aldri rette seg mot en oppgitt applikasjonsdatabase.
            await KjorMasterAsync($"ALTER DATABASE [{_navn}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_navn}];");
            _opprettet = false;
        }
    }
}
