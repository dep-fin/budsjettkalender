using Aarshjul.Application.Datautveksling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Maloverføringens serverinngang kontrollerer kretsens identitet ved hvert kall.</summary>
public sealed class MaloverfoeringKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    IMalDatautveksling utveksling)
{
    public async Task<MalImportForhaandsvisning> ForhaandsvisAsync(MalDatabase database, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        return await utveksling.ForhaandsvisAsync(database, ct);
    }

    public async Task<ImportResultat> ImporterAsync(MalDatabase database, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        return await utveksling.ImporterAsync(database, ct);
    }

    private async Task KrevAdministratorAsync()
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan overføre årsmalen.");
    }
}
