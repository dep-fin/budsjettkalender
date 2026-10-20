using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Tests;

/// <summary>
/// Holder en navngitt SQLite-minnedatabase i live. Fabrikken lager en ny kontekst og
/// tilkobling per operasjon; Db er en separat kontekst for testoppsett.
/// </summary>
public sealed class Testdatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public Testdatabase()
    {
        var tilkobling = $"Data Source=test-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(tilkobling);
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(tilkobling)
            .Options;

        Factory = new Testkontekstfabrikk(options);
        Db = Factory.CreateDbContext();
        Db.Database.EnsureCreated();
    }

    public AppDbContext Db { get; }
    public IDbContextFactory<AppDbContext> Factory { get; }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

internal sealed class Testkontekstfabrikk(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}

/// <summary>TimeProvider med fast «nå», for deterministiske dato-/historikktester.</summary>
public sealed class FastKlokke(DateOnly idag) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
        => new(idag.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
