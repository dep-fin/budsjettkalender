using Aarshjul.Infrastructure;
using Aarshjul.Testkontrakter;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aarshjul.Tests;

public sealed class FasemalTester : FasemalKontrakt
{
    protected override async Task<Fasedatabase> OpprettDatabaseAsync()
    {
        var database = new SqliteFasedatabase();
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    private sealed class SqliteFasedatabase : Fasedatabase
    {
        private readonly string _tilkobling = $"Data Source=faser-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _holder;
        public SqliteFasedatabase()
        {
            _holder = new SqliteConnection(_tilkobling);
            _holder.Open();
        }
        protected override DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling);
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
        }
        public override async Task InstallerLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER Testfasefeil BEFORE UPDATE ON Fasemaler
                BEGIN SELECT RAISE(ABORT, 'Testfeil ved fasemallagring'); END;
                """);
        }
        public override async Task FjernLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER Testfasefeil;");
        }
        public override ValueTask DisposeAsync() => _holder.DisposeAsync();
    }
}
