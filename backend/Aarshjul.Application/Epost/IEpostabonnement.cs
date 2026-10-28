namespace Aarshjul.Application.Epost;

/// <summary>Eget abonnement; Web henter brukeridentiteten fra autentisert forespørsel/krets.</summary>
public interface IEpostabonnement
{
    Task<EpostabonnementDto> HentAsync(string brukerId, CancellationToken ct = default);
    Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtarStandardvalg, CancellationToken ct = default);
    Task<EpostabonnementDto> SendNyAsync(string brukerId, CancellationToken ct = default);
    Task<EpostabonnementDto> BekreftAsync(string brukerId, string token, CancellationToken ct = default);
    Task<EpostabonnementDto> MeldAvAsync(string brukerId, CancellationToken ct = default);
}

public sealed class EpostabonnementKonflikt() : Exception("Abonnementet ble endret samtidig. Last inn status og prøv igjen.");

public sealed record EpostabonnementDto(string Status, string? Adresse, bool NyeFrister = true,
    bool VesentligeEndringer = false, bool AlleBudsjettaar = true, bool AlleKategorier = true,
    DateTimeOffset? Aktivert = null, DateTimeOffset? Endret = null);

/// <summary>Transporten får bare en adressebekreftelse, aldri fristdata. Token skal ikke logges.</summary>
public interface IBekreftelsespost
{
    bool Tilgjengelig { get; }
    Task SendAsync(string brukerId, string adresse, string token, CancellationToken ct = default);
}
