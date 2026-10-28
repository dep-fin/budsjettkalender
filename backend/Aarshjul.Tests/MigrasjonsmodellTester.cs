using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Aarshjul.Tests;

public sealed class MigrasjonsmodellTester
{
    [Fact]
    public void Siste_SQL_Server_migrasjon_bevarer_epost_og_internvern_uten_ventende_modellendringer()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=KunModell;Integrated Security=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrasjoner = db.GetService<IMigrationsAssembly>();
        var siste = migrasjoner.CreateMigration(migrasjoner.Migrations.Last().Value, db.Database.ProviderName!);
        var epost = siste.TargetModel.FindEntityType(typeof(Epostabonnement));
        Assert.NotNull(epost);
        Assert.True(epost.FindProperty(nameof(Epostabonnement.Versjon))!.IsConcurrencyToken);
        var runde = siste.TargetModel.FindEntityType(typeof(InternRunde))!;
        Assert.True(runde.FindProperty(nameof(InternRunde.SistSynkronisert))!.IsConcurrencyToken);
        Assert.Contains(runde.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Aar] IS NOT NULL");
        Assert.Contains(runde.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Aar] IS NULL");
        var oppgave = siste.TargetModel.FindEntityType(typeof(InterntGjoeremaal))!;
        Assert.True(oppgave.FindProperty(nameof(InterntGjoeremaal.Status))!.IsConcurrencyToken);
        Assert.True(oppgave.FindProperty(nameof(InterntGjoeremaal.ManueltEndret))!.IsConcurrencyToken);
    }
}
