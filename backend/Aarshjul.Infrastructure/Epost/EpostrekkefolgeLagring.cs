using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Aktiveres av abonnementsvalg og vellykket publisering, også fra godkjenningskøen.</summary>
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
            && (x.State == EntityState.Added || x.State == EntityState.Modified &&
                (x.Property(r => r.Status).OriginalValue != "Aktiv"
                 || x.Entity.NyeFrister && !x.Property(r => r.NyeFrister).OriginalValue
                 || x.Entity.VesentligeEndringer && !x.Property(r => r.VesentligeEndringer).OriginalValue
                 || x.Entity.UtvalgJson != x.Property(r => r.UtvalgJson).OriginalValue))).ToList();
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
                var nyAktivering = rad.State == EntityState.Added || rad.Property(r => r.Status).OriginalValue != "Aktiv";
                var utvalg = Abonnementsutvalg.Les(rad.Entity.UtvalgJson);
                rad.Entity.UtvalgJson = utvalg.Aktiver(Abonnementsutvalg.Les(rad.Property(r => r.UtvalgJson).OriginalValue), sekvens, nyAktivering).Json();
                var nye = nyAktivering || rad.Entity.NyeFrister && !rad.Property(r => r.NyeFrister).OriginalValue;
                var endringer = nyAktivering || rad.Entity.VesentligeEndringer && !rad.Property(r => r.VesentligeEndringer).OriginalValue;
                var endret = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Epostaktiveringer SET Sekvens = CASE WHEN {nye} = 1 THEN {sekvens} ELSE Sekvens END, Endringssekvens = CASE WHEN {endringer} = 1 THEN {sekvens} ELSE Endringssekvens END WHERE BrukerId = {rad.Entity.BrukerId}", ct);
                if (endret == 0)
                {
                    // Aktive EV02-abonnementer har implisitte grenser 0. En
                    // utvidelse må bevare den andre typens ventende hendelser.
                    var nyeGrense = nye ? sekvens : 0L;
                    var endringsgrense = endringer ? sekvens : 0L;
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO Epostaktiveringer (BrukerId, Sekvens, Endringssekvens) VALUES ({rad.Entity.BrukerId}, {nyeGrense}, {endringsgrense})", ct);
                }
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
