using Aarshjul.Application.Grupper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Gruppeforvaltningens serverinngang kontrollerer kretsens identitet ved hvert skriv.</summary>
public sealed class GruppeadministrasjonKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    IGruppetjeneste grupper)
{
    public async Task OpprettAsync(string kode, string navn, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        await grupper.OpprettAsync(kode, navn, ct);
    }

    public async Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default)
    {
        await KrevAdministratorAsync();
        await grupper.OppdaterAsync(kode, navn, aktiv, ct);
    }

    private async Task KrevAdministratorAsync()
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan vedlikeholde synlighetsgrupper.");
    }
}
