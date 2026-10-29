using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarshjul.SqlServerTests;

public sealed partial class DagligEpostTester
{
    [Fact]
    public async Task EV11_SQL_oppgraderer_EV10_additivt_og_bevarer_under_sending_uten_oppdiktet_starttid()
    {
        await using var database = new Sqldatabase(); await database.OpprettAsync("20261029170000_Epostadministratorstans");
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Epostoppsummeringer (Id, BrukerId, LokalDato, AktivertUtcTicks, Status, Versjon) VALUES ('10000000-0000-0000-0000-000000000001', 'legacy', '2030-01-01', 0, 'UnderSending', '20000000-0000-0000-0000-000000000001')");
        await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
        var drift = await db.Epostdrift.SingleAsync(); Assert.False(drift.Stanset); Assert.Null(drift.AktoerId);
        var plan = await db.Epostoppsummeringer.SingleAsync(); Assert.Equal("UnderSending", plan.Status); Assert.Null(plan.ForsoekStartetUtcTicks);
        Assert.Equal(0, plan.AntallForsoek); Assert.Empty(await db.Epostsendeforsoek.ToListAsync());
    }
}
