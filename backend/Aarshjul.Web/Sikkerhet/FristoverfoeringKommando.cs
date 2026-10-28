using Aarshjul.Application.Datautveksling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Fristoverføring autoriseres på server ved både kontroll og faktisk erstatning.</summary>
public sealed class FristoverfoeringKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    IFristDatautveksling utveksling)
{
    public async Task<FristImportForhaandsvisning> ForhaandsvisAsync(FristDatabase database, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        return await utveksling.ForhaandsvisAsync(database, ct);
    }

    public async Task<ImportResultat> ImporterAsync(FristDatabase database, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        return await utveksling.ImporterAsync(database, ct);
    }

    private async Task KrevAdministratorAsync()
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan overføre frister.");
    }
}
