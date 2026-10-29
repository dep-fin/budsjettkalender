using Aarshjul.Infrastructure;
using Aarshjul.Testkontrakter;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aarshjul.Tests;

public sealed class GodkjenningskonkurranseTester : GodkjenningskonkurranseKontrakt
{
    protected override async Task<Godkjenningsdatabase> OpprettDatabaseAsync()
    {
        var database = new SqliteGodkjenningsdatabase();
        await using var db = database.Fabrikk().CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    private sealed class SqliteGodkjenningsdatabase : Godkjenningsdatabase
    {
        private readonly string _tilkobling = $"Data Source=godkjenning-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _holder;

        public SqliteGodkjenningsdatabase()
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
                CREATE TRIGGER Testfeil BEFORE INSERT ON FristSynlighet
                BEGIN
                    SELECT CASE WHEN
                        (SELECT COUNT(*) FROM Frister) = 1 AND
                        (SELECT COUNT(*) FROM Varsler) = 1 AND
                        (SELECT COUNT(*) FROM NyFristHendelser WHERE Type = 'Ny') = 1 AND
                        (SELECT Verdi FROM Epostrekkefolge WHERE Id = 1) = 1 AND
                        (SELECT COUNT(*) FROM Forslag WHERE Status = 1) = 1
                    THEN RAISE(ABORT, 'Testfeil etter publiseringsskriv')
                    ELSE RAISE(ABORT, 'Testoppsett: publiseringsskriv mangler') END;
                END;
                """);
        }

        public override async Task FjernLagringsfeilAsync()
        {
            await using var db = Fabrikk().CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER Testfeil;");
        }

        public override ValueTask DisposeAsync() => _holder.DisposeAsync();
    }
}
