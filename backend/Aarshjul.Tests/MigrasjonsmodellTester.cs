using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Aarshjul.Tests;

public sealed class MigrasjonsmodellTester
{
    [Theory]
    [InlineData("20261028112544_UnikeInternrunder", false, false)]
    [InlineData("20261028133321_DagligEpostoppsummering", true, false)]
    [InlineData("20261028142358_OrdnetEpostaktivering", true, true)]
    public void Hvert_migrasjonsmaal_bevarer_levert_internvern_og_epostskjema_i_riktig_rekkefolge(
        string migrasjonId, bool oppsummering, bool ordnet) => KontrollerMaal(migrasjonId, oppsummering, ordnet, false);

    [Fact]
    public void EV04_maal_bevarer_tidligere_vern_og_samsvarer_med_siste_snapshot()
        => KontrollerMaal("20261028165341_VarseltyperOgEndringer", true, true, true);

    private static void KontrollerMaal(
        string migrasjonId, bool oppsummering, bool ordnet, bool endringer)
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=KunModell;Integrated Security=True").Options);
        var samling = db.GetService<IMigrationsAssembly>();
        var migrasjoner = samling.Migrations.ToArray();
        var indeks = Array.FindIndex(migrasjoner, m => m.Key == migrasjonId);
        Assert.True(indeks > 0);
        var migrasjon = samling.CreateMigration(migrasjoner[indeks].Value, db.Database.ProviderName!);
        var tidligere = samling.CreateMigration(migrasjoner[indeks - 1].Value, db.Database.ProviderName!);
        var initialiser = db.GetService<IModelRuntimeInitializer>();
        var maal = initialiser.Initialize(migrasjon.TargetModel, designTime: true);
        var foer = initialiser.Initialize(tidligere.TargetModel, designTime: true);

        var runde = maal.FindEntityType(typeof(InternRunde))!;
        Assert.True(runde.FindProperty(nameof(InternRunde.SistSynkronisert))!.IsConcurrencyToken);
        Assert.Contains(runde.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Aar] IS NULL");
        Assert.Contains(runde.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Aar] IS NOT NULL");
        var gjoeremaal = maal.FindEntityType(typeof(InterntGjoeremaal))!;
        Assert.True(gjoeremaal.FindProperty(nameof(InterntGjoeremaal.Status))!.IsConcurrencyToken);
        Assert.True(gjoeremaal.FindProperty(nameof(InterntGjoeremaal.ManueltEndret))!.IsConcurrencyToken);
        Assert.True(maal.FindEntityType(typeof(Epostabonnement))!
            .FindProperty(nameof(Epostabonnement.Versjon))!.IsConcurrencyToken);

        var hendelse = maal.FindEntityType(typeof(NyFristHendelse));
        var plan = maal.FindEntityType(typeof(Epostoppsummering));
        Assert.Equal(oppsummering, hendelse is not null);
        Assert.Equal(oppsummering, plan is not null);
        Assert.Equal(oppsummering, maal.FindEntityType(typeof(Oppsummeringshendelse)) is not null);
        if (oppsummering)
        {
            Assert.Empty(hendelse!.GetForeignKeys()); // Fristsletting må ikke slette hendelseshistorikken.
            Assert.Contains(hendelse.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == "FristId");
            Assert.Equal(endringer, hendelse.FindProperty("Type") is not null);
            Assert.Equal(endringer ? "[Type] = 'Ny'" : null, hendelse.GetIndexes().Single(i => i.IsUnique).GetFilter());
            Assert.Equal(endringer, maal.FindEntityType(typeof(Frist))!.FindProperty("Dato")!.IsConcurrencyToken);
            Assert.Contains(plan!.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { "BrukerId", "LokalDato" }));
            Assert.True(plan.FindProperty("Versjon")!.IsConcurrencyToken);
            Assert.Equal(ordnet, plan.FindProperty("Status")!.IsConcurrencyToken);
            Assert.Equal(ordnet, hendelse.FindProperty("Publiseringssekvens") is not null);
        }
        Assert.Equal(ordnet, maal.FindEntityType("Aarshjul.Infrastructure.Epost.Epostaktivering") is not null);
        Assert.Equal(ordnet, maal.FindEntityType("Aarshjul.Infrastructure.Epost.Epostrekkefolge") is not null);

        // Målmodellene må beskrive de faktiske, uendrede Up/Down-operasjonene.
        // UX16s ekstra duplikatkontroll er en datavakt, ikke en skjemaendring.
        var differ = db.GetService<IMigrationsModelDiffer>();
        Assert.Equal(Skjema(migrasjon.UpOperations),
            Skjema(differ.GetDifferences(foer.GetRelationalModel(), maal.GetRelationalModel())));
        Assert.Equal(Skjema(migrasjon.DownOperations),
            Skjema(differ.GetDifferences(maal.GetRelationalModel(), foer.GetRelationalModel())));

        if (migrasjonId == samling.Migrations.Last().Key)
        {
            var snapshot = initialiser.Initialize(samling.ModelSnapshot!.Model, designTime: true);
            Assert.False(differ.HasDifferences(maal.GetRelationalModel(), snapshot.GetRelationalModel()));
        }
    }

    // Kolonnerekkefølge og leverandørens midlertidige SQL-variabelnavn er uten
    // betydning for skjemaet. Sammenlikn faktiske kolonner, nøkler, filtre og seed.
    private static string[] Skjema(IReadOnlyList<MigrationOperation> operasjoner)
    {
        string Kolonne(AddColumnOperation c) => $"{c.Name}:{c.ColumnType}:{c.IsNullable}:{c.MaxLength}:{c.DefaultValue}:{c.DefaultValueSql}";
        return operasjoner.Where(o => o is not SqlOperation).Select(o => o switch
        {
            CreateTableOperation t => $"tabell {t.Name}|{string.Join(';', t.Columns.Select(Kolonne).Order())}"
                + $"|pk {string.Join(',', t.PrimaryKey!.Columns)}"
                + $"|fk {string.Join(';', t.ForeignKeys.Select(f => $"{string.Join(',', f.Columns)}>{f.PrincipalTable}({string.Join(',', f.PrincipalColumns!)}):{f.OnDelete}").Order())}"
                + $"|sjekk {string.Join(';', t.CheckConstraints.Select(c => $"{c.Name}:{c.Sql}").Order())}",
            DropTableOperation t => $"slett tabell {t.Name}",
            AddColumnOperation c => $"kolonne {c.Table}|{Kolonne(c)}",
            DropColumnOperation c => $"slett kolonne {c.Table}.{c.Name}",
            CreateIndexOperation i => $"indeks {i.Table}.{i.Name}|{string.Join(',', i.Columns)}|{i.IsUnique}|{i.Filter}",
            DropIndexOperation i => $"slett indeks {i.Table}.{i.Name}",
            InsertDataOperation d => $"seed {d.Table}|{string.Join(',', d.Columns)}|{string.Join(',', d.Values.Cast<object>())}",
            DeleteDataOperation d => $"slett seed {d.Table}|{string.Join(',', d.KeyColumns)}|{string.Join(',', d.KeyValues.Cast<object>())}",
            _ => throw new InvalidOperationException($"Uventet skjemaoperasjon: {o.GetType().Name}")
        }).Order(StringComparer.Ordinal).ToArray();
    }

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
