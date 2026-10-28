using Aarshjul.Application.Utskrift;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Eksportforhåndsvisning har samme administratorgrense som filendepunktene.</summary>
public sealed class EksportKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    FristEksportutvalg utvalg)
{
    public async Task<Eksportutvalg> HentAsync(bool alt, string? gruppeKode, DateOnly? fra, DateOnly? til, CancellationToken ct = default)
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException("Bare administrator kan kontrollere eksportutvalget.");
        return await utvalg.HentAsync(alt, gruppeKode, fra, til, ct);
    }
}
