using Aarshjul.Domain;

namespace Aarshjul.Application.Epost;

/// <summary>Eget abonnement; Web henter brukeridentiteten fra autentisert forespørsel/krets.</summary>
public interface IEpostabonnement
{
    Task<EpostabonnementDto> HentAsync(string brukerId, CancellationToken ct = default);
    Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtarStandardvalg, CancellationToken ct = default);
    Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtar, bool nyeFrister, bool vesentligeEndringer, CancellationToken ct = default);
    Task<EpostabonnementDto> EndreValgAsync(string brukerId, bool nyeFrister, bool vesentligeEndringer, Guid versjon, CancellationToken ct = default);
    /// <summary>Null eller tom samling betyr Alle, også fremtidige budsjettår. Union innen hvert utvalg, snitt mellom dem.</summary>
    Task<EpostabonnementDto> MeldPaaAsync(string brukerId, string adresse, bool godtar, bool nyeFrister, bool vesentligeEndringer,
        IReadOnlyCollection<int>? budsjettaar, IReadOnlyCollection<Kategori>? kategorier, CancellationToken ct = default);
    Task<EpostabonnementDto> EndreValgAsync(string brukerId, bool nyeFrister, bool vesentligeEndringer, Guid versjon,
        IReadOnlyCollection<int>? budsjettaar, IReadOnlyCollection<Kategori>? kategorier, CancellationToken ct = default);
    Task<EpostabonnementDto> SendNyAsync(string brukerId, CancellationToken ct = default);
    /// <summary>Bytter egen adresse og stanser varslingen til ny bekreftelse. Bevarer varselvalg og krever sist leste versjon.</summary>
    Task<EpostabonnementDto> EndreAdresseAsync(string brukerId, string adresse, bool egenadresseSamtykke, Guid versjon, CancellationToken ct = default);
    Task<EpostabonnementDto> BekreftAsync(string brukerId, string token, CancellationToken ct = default);
    Task<EpostabonnementDto> MeldAvAsync(string brukerId, CancellationToken ct = default);
    /// <summary>Uttrykkelig avmelding med en snever lenkerett. Gir aldri abonnement eller lesetilgang i retur.</summary>
    Task MeldAvMedLenkeAsync(string? token, CancellationToken ct = default);
}

public sealed class EpostabonnementKonflikt() : Exception("Abonnementet ble endret samtidig. Last inn status og prøv igjen.");

public sealed record EpostabonnementDto(string Status, string? Adresse, bool NyeFrister = true,
    bool VesentligeEndringer = false, bool AlleBudsjettaar = true, bool AlleKategorier = true,
    DateTimeOffset? Aktivert = null, DateTimeOffset? Endret = null, Guid Versjon = default,
    IReadOnlyList<int>? Budsjettaar = null, IReadOnlyList<Kategori>? Kategorier = null);

/// <summary>Transporten får bare en adressebekreftelse, aldri fristdata. Token skal ikke logges.</summary>
public interface IBekreftelsespost
{
    bool Tilgjengelig { get; }
    Task SendAsync(string brukerId, string adresse, string token, CancellationToken ct = default);
}
