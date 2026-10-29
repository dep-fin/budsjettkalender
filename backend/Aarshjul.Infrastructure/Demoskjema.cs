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
        var plankolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostoppsummeringer')").ToListAsync(ct);
        var fristkolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Frister')").ToListAsync(ct);
        var stanskolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostadministratorstanser')").ToListAsync(ct);
        var stansindekser = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_index_list('Epostadministratorstanser')").ToListAsync(ct);
        var stansnoekkel = await db.Database.SqlQueryRaw<int>("SELECT count(*) AS Value FROM pragma_table_info('Epostadministratorstanser') WHERE name = 'Id' AND pk = 1 AND upper(type) = 'INTEGER'").SingleAsync(ct);
        if (tabeller.Contains("Epostadministratorstanser")
            && (new[] { "Id", "BrukerId", "AktoerId", "Tidspunkt", "Handling" }.Except(stanskolonner).Any()
                || !stansindekser.Contains("IX_Epostadministratorstanser_BrukerId_Tidspunkt") || stansnoekkel != 1))
            throw new InvalidOperationException("Demo-databasens revisjonsskjema er ufullstendig. Eksisterende data er ikke endret.");
        var driftskolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostdrift')").ToListAsync(ct);
        var forsoekskolonner = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('Epostsendeforsoek')").ToListAsync(ct);
        var forsoeksindekser = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM pragma_index_list('Epostsendeforsoek')").ToListAsync(ct);
        var harEV11 = tabeller.Contains("Epostdrift") || tabeller.Contains("Epostsendeforsoek") || plankolonner.Contains("AntallForsoek");
        if (harEV11 && (new[] { "Id", "Stanset", "AktoerId", "EndretUtcTicks", "Versjon" }.Except(driftskolonner).Any()
            || new[] { "Id", "OppsummeringId", "Nummer", "StartetUtcTicks", "AvsluttetUtcTicks", "Status" }.Except(forsoekskolonner).Any()
            || new[] { "AntallForsoek", "ForsoekStartetUtcTicks", "NesteForsoekUtcTicks", "NesteOppslagUtcTicks" }.Except(plankolonner).Any()
            || !forsoeksindekser.Contains("IX_Epostsendeforsoek_OppsummeringId_Nummer")))
            throw new InvalidOperationException("Demo-databasens driftsskjema er ufullstendig. Eksisterende data er ikke endret.");
        if (hendelseskolonner.Contains("Type") && abonnementskolonner.Contains("UtvalgJson")
            && tabeller.Contains("FristHendelsessynlighet") && fristkolonner.Contains("ManuellKildeUrl")
            && plankolonner.Contains("AvmeldingsHash") && plankolonner.Contains("Avmeldingssekvens")
            && tabeller.Contains("Epostadministratorstanser") && tabeller.Contains("Epostdrift")
            && tabeller.Contains("Epostsendeforsoek") && plankolonner.Contains("AntallForsoek")) return;
        if (!tabeller.Contains("Epostabonnementer"))
            throw new InvalidOperationException("Demo-databasen er eldre enn EV02. Bruk en ny disponibel demo-database; eksisterende data er ikke endret.");
        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);
        var migrasjoner = new List<Migration>();
        if (!tabeller.Contains("NyFristHendelser")) migrasjoner.Add(new Migrations.DagligEpostoppsummering());
        if (!tabeller.Contains("Epostrekkefolge")) migrasjoner.Add(new Migrations.OrdnetEpostaktivering());
        if (!hendelseskolonner.Contains("Type")) migrasjoner.Add(new Migrations.VarseltyperOgEndringer());
        if (!abonnementskolonner.Contains("UtvalgJson")) migrasjoner.Add(new Migrations.Abonnementsutvalg());
        if (!tabeller.Contains("FristHendelsessynlighet")) migrasjoner.Add(new Migrations.HistoriskSlettingssynlighet());
        if (!fristkolonner.Contains("ManuellKildeUrl")) migrasjoner.Add(new Migrations.ManuellKildelenke());
        if (!plankolonner.Contains("AvmeldingsHash") || !plankolonner.Contains("Avmeldingssekvens")) migrasjoner.Add(new Migrations.Avmeldingslenke());
        if (!tabeller.Contains("Epostadministratorstanser")) migrasjoner.Add(new Migrations.Epostadministratorstans());
        if (!tabeller.Contains("Epostdrift")) migrasjoner.Add(new Migrations.Epostdriftsvern());
        foreach (var migrasjon in migrasjoner)
        {
            // SQLite krever INTEGER PRIMARY KEY for databasegenerert revisjons-ID.
            foreach (var tabell in migrasjon.UpOperations.OfType<CreateTableOperation>().Where(t => t.Name == "Epostadministratorstanser"))
            {
                var id = tabell.Columns.Single(c => c.Name == "Id");
                id.ColumnType = "INTEGER";
                id["Sqlite:Autoincrement"] = true;
            }
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
