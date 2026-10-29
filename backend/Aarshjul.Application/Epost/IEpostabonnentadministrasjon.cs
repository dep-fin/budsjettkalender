using Aarshjul.Domain;

namespace Aarshjul.Application.Epost;

/// <summary>Registrerte påmeldinger. Web autoriserer administrator og binder aktøren til innloggingen.</summary>
public interface IEpostabonnentadministrasjon
{
    Task<IReadOnlyList<EpostabonnentDto>> HentAsync(CancellationToken ct = default);
    Task StansAsync(string brukerId, Guid versjon, bool bekreftet, string aktoerId, CancellationToken ct = default);
}

public sealed record EpostabonnentDto(string BrukerId, string Navn, string Adresse, string Status,
    bool AdresseBekreftet, bool NyeFrister, bool VesentligeEndringer,
    IReadOnlyList<int>? Budsjettaar, IReadOnlyList<Kategori>? Kategorier,
    DateTimeOffset Opprettet, DateTimeOffset Endret, DateTimeOffset? Aktivert, Guid Versjon,
    string? SistStansetAv, DateTimeOffset? SistStanset);
