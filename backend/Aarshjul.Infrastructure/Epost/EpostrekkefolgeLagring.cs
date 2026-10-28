using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Aktiveres bare av adressebekreftelse og direkte manuell publisering.</summary>
internal sealed class EpostrekkefolgeLagring : SaveChangesInterceptor
{
    internal bool Aktiv { get; set; }
    private IDbContextTransaction? _egenTransaksjon;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (!Aktiv) return result;
        var db = (AppDbContext)data.Context!;
        var hendelser = db.ChangeTracker.Entries<NyFristHendelse>().Where(x => x.State == EntityState.Added).ToList();
        var aktiveringer = db.ChangeTracker.Entries<Epostabonnement>().Where(x => x.Entity.Status == "Aktiv"
            && (x.State == EntityState.Added || x.State == EntityState.Modified && x.Property(r => r.Status).OriginalValue != "Aktiv")).ToList();
        if (hendelser.Count == 0 && aktiveringer.Count == 0) return result;
        try
        {
            if (db.Database.CurrentTransaction is null) _egenTransaksjon = await db.Database.BeginTransactionAsync(ct);
            var antall = await db.Database.ExecuteSqlRawAsync("UPDATE Epostrekkefolge SET Verdi = Verdi + 1 WHERE Id = 1", ct);
            if (antall != 1) throw new InvalidOperationException("Databasens e-postrekkefølge mangler.");
            // UPDATE holder skrivelåsen frem til commit på begge SQL-leverandører.
            // Nummeret er ingen klokke og kan ikke passeres av en senere commit.
            var sekvens = await db.Set<Epostrekkefolge>().AsNoTracking().Select(x => x.Verdi).SingleAsync(ct);
            foreach (var rad in aktiveringer)
            {
                var endret = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Epostaktiveringer SET Sekvens = {sekvens} WHERE BrukerId = {rad.Entity.BrukerId}", ct);
                if (endret == 0) await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Epostaktiveringer (BrukerId, Sekvens) VALUES ({rad.Entity.BrukerId}, {sekvens})", ct);
            }
            foreach (var rad in hendelser) rad.Entity.Publiseringssekvens = sekvens;
        }
        catch
        {
            await AvsluttAsync(false, CancellationToken.None);
            throw;
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
    {
        await AvsluttAsync(true, ct);
        return result;
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken ct = default)
        => AvsluttAsync(false, CancellationToken.None);
    public override Task SaveChangesCanceledAsync(DbContextEventData data, CancellationToken ct = default)
        => AvsluttAsync(false, CancellationToken.None);

    private async Task AvsluttAsync(bool commit, CancellationToken ct)
    {
        var transaksjon = _egenTransaksjon;
        if (transaksjon is null) return;
        _egenTransaksjon = null;
        try { if (commit) await transaksjon.CommitAsync(ct); }
        finally { await transaksjon.DisposeAsync(); } // Dispose ruller tilbake ved feil/avbrudd.
    }
}
