namespace Aarshjul.Application.Epost;

/// <summary>Beskyttet driftsstyring uten e-postinnhold eller hemmeligheter.</summary>
public interface IEpostdrift
{
    Task<EpostdriftDto> HentAsync(CancellationToken ct = default);
    Task SettStansAsync(bool stanset, Guid versjon, bool bekreftet, string aktoerId, CancellationToken ct = default);
    Task<int> RyddAsync(CancellationToken ct = default);
}
public sealed record EpostdriftDto(bool Stanset, Guid Versjon, string? EndretAv, long? EndretUtcTicks,
    IReadOnlyList<EpoststatusAntall> Statuser, IReadOnlyList<EpostforsoekDto> SisteForsoek, IReadOnlyList<EpostplanstatusDto> UavsluttedePlaner);
public sealed record EpoststatusAntall(string Status, int Antall);
public sealed record EpostforsoekDto(Guid OppsummeringId, int Nummer, string Status, long StartetUtcTicks, long? AvsluttetUtcTicks);
public sealed record EpostplanstatusDto(Guid Id, DateOnly LokalDato, string Status, string? Utsettelsesgrunn, int AntallForsoek, long? NesteForsoekUtcTicks, long? NesteOppslagUtcTicks);
