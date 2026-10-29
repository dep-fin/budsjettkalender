using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Aarshjul.Tests;

public sealed partial class DagligEpostTester
{
    [Fact]
    public async Task EV11_demooppgradering_er_additiv_og_idempotent_med_felles_aktiv_driftsrad()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV10SkjemaAsync();
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Epostoppsummeringer (Id, BrukerId, LokalDato, AktivertUtcTicks, Status, Versjon) VALUES ('10000000-0000-0000-0000-000000000001', 'legacy', '2030-01-01', 0, 'UnderSending', '20000000-0000-0000-0000-000000000001')");
        await Demoskjema.SikreAsync(db); await Demoskjema.SikreAsync(db);
        var drift = await db.Epostdrift.SingleAsync(); Assert.False(drift.Stanset); Assert.Null(drift.AktoerId);
        var plan = await db.Epostoppsummeringer.SingleAsync(); Assert.Equal("UnderSending", plan.Status); Assert.Null(plan.ForsoekStartetUtcTicks);
        Assert.Equal(0, plan.AntallForsoek); Assert.Empty(await db.Epostsendeforsoek.ToListAsync());
    }
    [Fact]
    public async Task EV11_demooppgradering_avviser_delvis_driftsskjema_uten_aa_fylle_eller_slette()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV10SkjemaAsync();
        await using var db = database.Fabrikk().CreateDbContext(); await db.Database.ExecuteSqlRawAsync("CREATE TABLE Epostdrift (Id INTEGER PRIMARY KEY)");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Demoskjema.SikreAsync(db));
        Assert.DoesNotContain("Epostsendeforsoek", await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync());
    }
    [Fact]
    public async Task EV11_demooppgradering_ruller_nye_kolonner_tabeller_og_seed_tilbake_ved_sen_feil()
    {
        await using var database = new Sqlitedatabase(); await database.OpprettEV10SkjemaAsync();
        await using (var db = database.FabrikkMedKommando(new FeilEV11Indeks()).CreateDbContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Demoskjema.SikreAsync(db));
        await using var kontroll = database.Fabrikk().CreateDbContext();
        Assert.DoesNotContain("Epostdrift", await kontroll.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync());
        Assert.DoesNotContain("AntallForsoek", await kontroll.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostoppsummeringer')").ToListAsync());
        await Demoskjema.SikreAsync(kontroll); await Demoskjema.SikreAsync(kontroll); Assert.Single(await kontroll.Epostdrift.ToListAsync());
    }
    private sealed class FeilEV11Indeks : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (command.CommandText.Contains("IX_Epostsendeforsoek", StringComparison.Ordinal)) throw new InvalidOperationException("Kontrollert sen migrasjonsfeil");
            return ValueTask.FromResult(result);
        }
    }
}
