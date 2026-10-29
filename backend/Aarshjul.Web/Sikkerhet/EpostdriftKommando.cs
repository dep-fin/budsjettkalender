using Aarshjul.Application.Epost;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

public sealed class EpostdriftKommando(AuthenticationStateProvider tilstand, IAuthorizationService autorisasjon, IEpostdrift drift)
{
    public async Task<EpostdriftDto> HentAsync(CancellationToken ct = default)
    { await AutoriserAsync(ct); return await drift.HentAsync(ct); }
    public async Task SettStansAsync(bool stanset, Guid versjon, bool bekreftet, CancellationToken ct = default)
        => await drift.SettStansAsync(stanset, versjon, bekreftet, await AutoriserAsync(ct), ct);
    private async Task<string> AutoriserAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan styre e-postdrift.");
        return EpostabonnentadministrasjonKommando.AktoerId(bruker) ?? throw new UnauthorizedAccessException("Stabil innlogget identitet kreves.");
    }
}
