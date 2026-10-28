using System.Globalization;
using Aarshjul.Application.Brukere;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Brukere;

public sealed class AdministratoroversiktTjeneste(IDbContextFactory<AppDbContext> dbFactory) : IAdministratoroversikt
{
    public async Task<IReadOnlyList<AdministratortilgangDto>> HentAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var administratorer = await db.Brukere.AsNoTracking()
            .Where(b => b.Funksjonsrolle == Funksjonsrolle.Administrator)
            .Select(b => new AdministratortilgangDto(b.Id, b.Navn, b.AdminViaEntra))
            .ToListAsync(ct);
        return administratorer.OrderBy(b => b.Navn, StringComparer.Create(CultureInfo.GetCultureInfo("nb-NO"), true))
            .ThenBy(b => b.BrukerId, StringComparer.Ordinal).ToArray();
    }
}
