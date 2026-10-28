using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Aarshjul.Infrastructure;

/// <summary>Additiv oppgradering av en lokal EV02-demo. SQL Server bruker vanlig migrasjonskjede.</summary>
public static class Demoskjema
{
    public static async Task SikreAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Database.EnsureCreatedAsync(ct)) return;
        var tabeller = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync(ct);
        var hendelseskolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('NyFristHendelser')").ToListAsync(ct);
        var abonnementskolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostabonnementer')").ToListAsync(ct);
        if (hendelseskolonner.Contains("Type") && abonnementskolonner.Contains("UtvalgJson")) return;
        if (!tabeller.Contains("Epostabonnementer"))
            throw new InvalidOperationException("Demo-databasen er eldre enn EV02. Bruk en ny disponibel demo-database; eksisterende data er ikke endret.");
        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        var migrasjoner = new List<Migration>();
        if (!tabeller.Contains("NyFristHendelser")) migrasjoner.Add(new Migrations.DagligEpostoppsummering());
        if (!tabeller.Contains("Epostrekkefolge")) migrasjoner.Add(new Migrations.OrdnetEpostaktivering());
        if (!hendelseskolonner.Contains("Type")) migrasjoner.Add(new Migrations.VarseltyperOgEndringer());
        if (!abonnementskolonner.Contains("UtvalgJson")) migrasjoner.Add(new Migrations.Abonnementsutvalg());
        foreach (var migrasjon in migrasjoner)
        {
            // Migrasjonene genereres for produksjonens SQL Server. SQLite bruker
            // TEXT for ubundet tekst; nvarchar(max) er ingen gyldig SQLite-type.
            foreach (var kolonne in migrasjon.UpOperations.OfType<AddColumnOperation>().Where(x => x.ColumnType == "nvarchar(max)"))
                kolonne.ColumnType = "TEXT";
            foreach (var kommando in db.GetService<IMigrationsSqlGenerator>().Generate(migrasjon.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(kommando.CommandText, ct);
        }
        await transaksjon.CommitAsync(ct);
    }
}
