using Aarshjul.Infrastructure;
using Aarshjul.Testkontrakter;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Aarshjul.Infrastructure.Internkalender;
using Aarshjul.Infrastructure.Epost;
using System.Text.Json;
using Xunit;

namespace Aarshjul.SqlServerTests;

public sealed class InternkalenderLagringsvernTester : InternkalenderSynkKontrakt
{
    [Fact]
    public async Task Oppgradering_bevarer_eksisterende_runder_gjoeremaal_ansvar_fullfoering_og_publiserte_data()
    {
        await using var database = new SqlServerInternkalenderdatabase();
        await database.OpprettAsync("20261028083448_Epostabonnement");
        await FasedataBevis.LeggTilHistorikkAsync(database.Fabrikk());
        var fristerFoer = await FasedataBevis.HentAsync(database.Fabrikk());
        var post = new KontrollertBekreftelsespost();
        var epost = new EpostabonnementTjeneste(EpostoppgraderingBevis.HistoriskFabrikk(database.Fabrikk()), TimeProvider.System, post);
        await epost.MeldPaaAsync("aktiv-foer-ux16", "aktiv@fin.dep.no", true);
        // Autentisk aktiv EV02-rad før EV03s koordinasjonstabeller finnes.
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var naa = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Epostabonnementer SET Status = 'Aktiv', Aktivert = {naa}, Endret = {naa}, BekreftelsesHash = NULL, BekreftelseUtloeper = NULL WHERE BrukerId = 'aktiv-foer-ux16'");
        }
        var aktivFoer = await epost.HentAsync("aktiv-foer-ux16");
        var venterFoer = await epost.MeldPaaAsync("venter-foer-ux16", "venter@fin.dep.no", true);
        IInternkalender kal = new InternkalenderTjeneste(database.Fabrikk(), TimeProvider.System);
        var runde = await kal.OpprettRundeAsync(Rundetype.Rnb, 2027, "Anna");
        var ovrig = await kal.HentEllerOpprettOvrigAsync("Bjørn");
        var oppgave = await kal.OpprettGjoeremaalAsync(runde, new()
        {
            Tittel = "Historisk oppgave", Notat = "Bevar hele oppgaven",
            Tidfestingstype = Tidfestingstype.TentativMaaned, Dato = new DateOnly(2027, 5, 1),
            Datokvalifikator = Datokvalifikator.Ultimo,
            Ansvarlige = [new("sbr-a", "Anna"), new(null, "Ekstern")]
        });
        await kal.FullfoerAsync(oppgave, "sbr-b", "Bjørn");
        await kal.HurtiglaggAsync(ovrig, "Stående oppgave");
        var runderFoer = JsonSerializer.Serialize(await kal.HentRunderAsync());
        var oppgaverFoer = JsonSerializer.Serialize(await kal.HentGjoeremaalAsync(runde, null));
        var ovrigFoer = JsonSerializer.Serialize(await kal.HentGjoeremaalAsync(ovrig, null));
        await using (var db = database.Fabrikk().CreateDbContext()) await db.Database.MigrateAsync();
        epost = new EpostabonnementTjeneste(database.Fabrikk(), TimeProvider.System, post);
        Assert.Equal(runderFoer, JsonSerializer.Serialize(await kal.HentRunderAsync()));
        Assert.Equal(oppgaverFoer, JsonSerializer.Serialize(await kal.HentGjoeremaalAsync(runde, null)));
        Assert.Equal(ovrigFoer, JsonSerializer.Serialize(await kal.HentGjoeremaalAsync(ovrig, null)));
        Assert.Equal(fristerFoer, await FasedataBevis.HentAsync(database.Fabrikk()));
        Assert.Equal(ovrig, await kal.HentEllerOpprettOvrigAsync("Anna"));
        Assert.Equal(aktivFoer, await epost.HentAsync("aktiv-foer-ux16"));
        Assert.Equal(venterFoer, await epost.HentAsync("venter-foer-ux16"));
        Assert.Equal("Aktiv", (await epost.BekreftAsync("venter-foer-ux16", post.Hent("venter-foer-ux16")!.Token)).Status);
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var anvendte = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Equal(new[] { "20261028083448_Epostabonnement", "20261028112544_UnikeInternrunder",
                "20261028133321_DagligEpostoppsummering", "20261028142358_OrdnetEpostaktivering",
                "20261028165341_VarseltyperOgEndringer", "20261028193853_Abonnementsutvalg" }, anvendte.TakeLast(6));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oppgradering_avviser_eldre_duplikater_med_veiledning_og_bevarer_alt(bool ovrig)
    {
        await using var database = new SqlServerInternkalenderdatabase();
        await database.OpprettAsync("20261028083448_Epostabonnement");
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            // Historisk datagrunnlag som kunne oppstå før unikhetsvernet.
            db.InternRunder.AddRange(new InternRunde { Id = Guid.NewGuid(), Rundetype = ovrig ? Rundetype.Ovrig : Rundetype.Rnb, Aar = ovrig ? null : 2027 },
                new InternRunde { Id = Guid.NewGuid(), Rundetype = ovrig ? Rundetype.Ovrig : Rundetype.Rnb, Aar = ovrig ? null : 2027 });
            await db.SaveChangesAsync();
        }
        IInternkalender kal = new InternkalenderTjeneste(database.Fabrikk(), TimeProvider.System);
        var runder = await kal.HentRunderAsync();
        foreach (var runde in runder) await kal.HurtiglaggAsync(runde.Id, $"Behold {runde.Id}");
        await using (var db = database.Fabrikk().CreateDbContext())
        {
            var feil = await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());
            Assert.Contains("Ta sikkerhetskopi og avklar", feil.Message);
            Assert.DoesNotContain("20261028112544_UnikeInternrunder", await db.Database.GetAppliedMigrationsAsync());
        }
        Assert.Equal(runder.Select(r => r.Id).Order(), (await kal.HentRunderAsync()).Select(r => r.Id).Order());
        foreach (var runde in runder) Assert.Single(await kal.HentGjoeremaalAsync(runde.Id, null));
    }

    protected override async Task<Internkalenderdatabase> OpprettDatabaseAsync()
    {
        var database = new SqlServerInternkalenderdatabase();
        try { await database.OpprettAsync(); return database; }
        catch { await database.DisposeAsync(); throw; }
    }
    private sealed class SqlServerInternkalenderdatabase : Internkalenderdatabase
    {
        private readonly string _navn = $"AarshjulIntern_{Guid.NewGuid():N}";
        private readonly string _master;
        private readonly string _tilkobling;
        private bool _opprettet;
        public SqlServerInternkalenderdatabase()
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
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_tilkobling, sql => sql.MaxBatchSize(1));
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
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
            // Navnet kommer bare fra dette objektet, aldri en oppgitt applikasjonsdatabase.
            await KjorMasterAsync($"ALTER DATABASE [{_navn}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_navn}];");
            _opprettet = false;
        }
    }
}
