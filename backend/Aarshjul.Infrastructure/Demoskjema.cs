using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Aarshjul.Infrastructure;

/// <summary>Additiv oppgradering av en lokal EV02-demo. SQL Server bruker vanlig migrasjonskjede.</summary>
public static class Demoskjema
{
    public static async Task SikreAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Database.EnsureCreatedAsync(ct)) return;
        var tabeller = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync(ct);
        if (tabeller.Contains("Epostrekkefolge")) return;
        if (!tabeller.Contains("Epostabonnementer"))
            throw new InvalidOperationException("Demo-databasen er eldre enn EV02. Bruk en ny disponibel demo-database; eksisterende data er ikke endret.");
        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        var migrasjoner = tabeller.Contains("NyFristHendelser")
            ? new Migration[] { new Migrations.OrdnetEpostaktivering() }
            : [new Migrations.DagligEpostoppsummering(), new Migrations.OrdnetEpostaktivering()];
        foreach (var migrasjon in migrasjoner)
            foreach (var kommando in db.GetService<IMigrationsSqlGenerator>().Generate(migrasjon.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(kommando.CommandText, ct);
        await transaksjon.CommitAsync(ct);
    }
}
