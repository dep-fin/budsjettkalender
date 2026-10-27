using Aarshjul.Application.Faser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Serverinngangen som editoren bruker. Identiteten hentes fra kretsen ved hver lagring.</summary>
public sealed class FasemalKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    IFasemal fasemal)
{
    public async Task<FasemalDto> OppdaterAsync(Guid forventetVersjon, IReadOnlyList<FaseDto> faser,
        int sisteMaanedInkludert, CancellationToken ct = default)
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan lagre den felles fasemalen.");

        return await fasemal.OppdaterAsync(forventetVersjon, faser, sisteMaanedInkludert, ct);
    }
}
