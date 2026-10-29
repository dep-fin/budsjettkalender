using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Den korte grensen mellom siste abonnementslesing og transportens mottak.</summary>
internal static class Epostoverlevering
{
    internal static async Task<IDbContextTransaction> LaasAsync(AppDbContext db, string brukerId, CancellationToken ct)
    {
        var transaksjon = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Samme låsrekkefølge som aktivering: rekkefølge før abonnement.
            // Ingen sekvens eller abonnementsversjon endres av disse låsene.
            if (await db.Database.ExecuteSqlRawAsync("UPDATE Epostrekkefolge SET Verdi = Verdi WHERE Id = 1", ct) != 1)
                throw new InvalidOperationException("Databasens e-postrekkefølge mangler.");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Epostabonnementer SET Versjon = Versjon WHERE BrukerId = {brukerId}", ct);
            return transaksjon;
        }
        catch { await transaksjon.DisposeAsync(); throw; }
    }
}
