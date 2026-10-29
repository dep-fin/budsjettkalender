using System.Security.Claims;
using Aarshjul.Application.Epost;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Autoriserer på nytt for hver kretsoperasjon; oppslag endrer aldri konto eller roller.</summary>
public sealed class EpostabonnentadministrasjonKommando(AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon, IEpostabonnentadministrasjon tjeneste)
{
    public async Task<IReadOnlyList<EpostabonnentDto>> HentAsync(CancellationToken ct = default)
    {
        await AutoriserAsync(ct);
        return await tjeneste.HentAsync(ct);
    }

    public async Task StansAsync(string brukerId, Guid versjon, bool bekreftet, CancellationToken ct = default)
        => await tjeneste.StansAsync(brukerId, versjon, bekreftet, await AutoriserAsync(ct), ct);

    private async Task<string> AutoriserAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan forvalte e-postabonnenter.");
        return AktoerId(bruker) ?? throw new UnauthorizedAccessException("Stabil innlogget identitet kreves.");
    }

    internal static string? AktoerId(ClaimsPrincipal bruker)
    {
        var id = bruker.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                 ?? bruker.FindFirst("oid")?.Value ?? bruker.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? bruker.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(id) || id.Length > 128 ? null : id;
    }
}
