using Aarshjul.Infrastructure;
using Aarshjul.Testkontrakter;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aarshjul.Tests;

public sealed class InternkalenderLagringsvernTester : InternkalenderSynkKontrakt
{
    protected override async Task<Internkalenderdatabase> OpprettDatabaseAsync()
    {
        var database = new SqliteInternkalenderdatabase();
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }
    private sealed class SqliteInternkalenderdatabase : Internkalenderdatabase
    {
        private readonly string _tilkobling = $"Data Source=intern-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _holder;
        public SqliteInternkalenderdatabase()
        {
            _holder = new(_tilkobling);
            _holder.Open();
        }
        protected override DbContextOptions<AppDbContext> Alternativer(SaveChangesInterceptor? port)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_tilkobling);
            if (port is not null) builder.AddInterceptors(port);
            return builder.Options;
        }
        public override ValueTask DisposeAsync() => _holder.DisposeAsync();
    }
}
