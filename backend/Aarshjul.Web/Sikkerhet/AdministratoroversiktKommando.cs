using System.Security.Claims;
using Aarshjul.Application.Brukere;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Administratoroversiktens serverinngang autoriserer hver lesing mot kretsens identitet.</summary>
public sealed class AdministratoroversiktKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    IAdministratoroversikt oversikt)
{
    public async Task<IReadOnlyList<Administratorrad>> HentAsync(CancellationToken ct = default)
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan lese administratoroversikten.");

        // Samme stabile claimprioritet som brukeroppslaget, uten å opprette eller synkronisere brukere.
        var id = bruker.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                 ?? bruker.FindFirst("oid")?.Value
                 ?? bruker.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? bruker.FindFirst("sub")?.Value;
        return (await oversikt.HentAsync(ct)).Select(b =>
            new Administratorrad(b.Navn, b.AdminViaEntra, id is not null && b.BrukerId == id)).ToArray();
    }
}

public sealed record Administratorrad(string Navn, bool AdminViaEntra, bool ErDeg);
