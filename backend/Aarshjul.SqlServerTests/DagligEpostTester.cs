using Aarshjul.Infrastructure;
using Aarshjul.Domain;
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
    public async Task EV07_migrasjon_fra_EV05_bevarer_data_og_planer_uten_historisk_tilbakefylling()
    {
        await using var database = new Sqldatabase(); await database.OpprettAsync("20261028193853_Abonnementsutvalg");
        await EpostoppgraderingBevis.LeggTilAsync(database);
        const string utvalg = """{"Budsjettaar":null,"Kategorier":null,"Grenser":[{"Budsjettaar":null,"Kategorier":null,"Sekvens":0}]}""";
        await using (var oppsett = database.Fabrikk().CreateDbContext())
            await oppsett.Database.ExecuteSqlInterpolatedAsync($"UPDATE Epostabonnementer SET UtvalgJson = {utvalg} WHERE BrukerId = 'eldre'");
        var plan = await EpostoppgraderingBevis.LeggTilEV04PlanAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varsling = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
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
    public async Task EV05_migrasjon_bevarer_EV04_og_fullfoerer_ventende_plan_med_legacy_metadata()
    {
        await using var database = new Sqldatabase(); await database.OpprettAsync("20261028165341_VarseltyperOgEndringer");
        await EpostoppgraderingBevis.LeggTilAsync(database);
        var plan = await EpostoppgraderingBevis.LeggTilEV04PlanAsync(database);
        var foer = await EpostoppgraderingBevis.HentAsync(database);
        var varslingFoer = await EpostoppgraderingBevis.HentVarslingsdataAsync(database);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.All(await db.Epostabonnementer.ToListAsync(), a => Assert.Null(a.UtvalgJson));
        }
        Assert.Equal(foer, await EpostoppgraderingBevis.HentAsync(database));
        Assert.Equal(varslingFoer, await EpostoppgraderingBevis.HentVarslingsdataAsync(database));
        await EpostoppgraderingBevis.BekreftEV05PlanAsync(database, plan);
    }
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
            Assert.Equal(new[] { "20261028133321_DagligEpostoppsummering", "20261028142358_OrdnetEpostaktivering", "20261028165341_VarseltyperOgEndringer", "20261028193853_Abonnementsutvalg", "20261029092000_HistoriskSlettingssynlighet" },
                (await db.Database.GetAppliedMigrationsAsync()).TakeLast(5));
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
