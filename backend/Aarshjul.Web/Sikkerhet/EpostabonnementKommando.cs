using Aarshjul.Application.Epost;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Alle selvbetjeningskall henter samme autentiserte kretsidentitet på server.</summary>
public sealed class EpostabonnementKommando(AuthenticationStateProvider tilstand,
    Gjeldendebrukerkilde brukerkilde, IEpostabonnement abonnement)
{
    public Task<EpostabonnementDto> HentAsync(CancellationToken ct = default)
        => KjorAsync(id => abonnement.HentAsync(id, ct), ct);
    public Task<EpostabonnementDto> MeldPaaAsync(string adresse, bool godtarStandardvalg, CancellationToken ct = default)
        => KjorAsync(id => abonnement.MeldPaaAsync(id, adresse, godtarStandardvalg, ct), ct);
    public Task<EpostabonnementDto> SendNyAsync(CancellationToken ct = default)
        => KjorAsync(id => abonnement.SendNyAsync(id, ct), ct);
    public Task<EpostabonnementDto> BekreftAsync(string token, CancellationToken ct = default)
        => KjorAsync(id => abonnement.BekreftAsync(id, token, ct), ct);
    public Task<EpostabonnementDto> MeldAvAsync(CancellationToken ct = default)
        => KjorAsync(id => abonnement.MeldAvAsync(id, ct), ct);

    private async Task<EpostabonnementDto> KjorAsync(Func<string, Task<EpostabonnementDto>> handling, CancellationToken ct)
    {
        if ((await tilstand.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Logg inn for å administrere eget abonnement.");
        return await handling((await brukerkilde.HentAsync(ct)).Id);
    }
}
