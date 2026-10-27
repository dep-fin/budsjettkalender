using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>En gjentaksregel slik den vises i mal-oversikten.</summary>
public sealed record MalRegelDto(
    Guid Id,
    string Loep,
    string Tittel,
    Kategori Kategori,
    Regeltype Regeltype,
    string Regelparametre,
    bool Valgaarssensitiv);

/// <summary>Inndata fra mal-skjemaet (opprett/rediger en gjentaksregel).</summary>
public sealed class MalRegelInndata
{
    public string Loep { get; set; } = "";
    public string Tittel { get; set; } = "";
    public Kategori Kategori { get; set; } = Kategori.Budsjett;
    public Regeltype Regeltype { get; set; } = Regeltype.FastDato;

    /// <summary>Regelparametre som JSON, avhengig av <see cref="Regeltype"/> (kravdok. 3.3).</summary>
    public string Regelparametre { get; set; } = "{\"maaned\":1,\"dag\":1,\"aar_forskyvning\":0}";

    public bool Valgaarssensitiv { get; set; }
}

/// <summary>
/// Utkastets beregningsresultat for ett budsjettår. Ved månedspresisjon er <see cref="Dato"/>
/// bare beregningens referansepunkt; dagen skal ikke presenteres som en fastsatt frist.
/// </summary>
public sealed record MalRegelForhaandsvisning(
    int Budsjettaar,
    DateOnly? Dato,
    Datopresisjon Presisjon,
    bool Tentativ,
    bool Valgaarsflagg,
    string? Feil)
{
    public bool ErFeil => Feil is not null;
}

/// <summary>
/// Forvalter årsmalen (kravdok. 3.3, 5.4): gjentaksreglene «generér neste år» bygger på. Alle
/// handlinger er administratorhandlinger (policy håndheves i web-laget). Ugyldig regeltype,
/// manglende parameterfelter og ugyldige parameterverdier gir <see cref="Valideringsfeil"/>
/// før malen endres. Manuell lagring krever også løp og tittel.
/// </summary>
public interface IMaltjeneste
{
    Task<IReadOnlyList<MalRegelDto>> HentAlleAsync(CancellationToken ct = default);
    Task<MalRegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Beregner utkastet for fem budsjettår fra <paramref name="foersteBudsjettaar"/> (1–9995)
    /// sammen med den lagrede malen, uten lagring av regler, forslag eller frister.
    /// <paramref name="id"/> erstatter samme regel i beregningsgrunnlaget; null prøver en ny regel.
    /// Ugyldige malfelter eller en ukjent ID gir <see cref="Valideringsfeil"/>. Beregningsfeil
    /// returneres per år og hindrer ikke lagring av en ellers gyldig mal.
    /// </summary>
    Task<IReadOnlyList<MalRegelForhaandsvisning>> ForhaandsvisAsync(
        MalRegelInndata inndata, int foersteBudsjettaar, Guid? id = null, CancellationToken ct = default);

    Task<Guid> OpprettAsync(MalRegelInndata inndata, CancellationToken ct = default);
    Task OppdaterAsync(Guid id, MalRegelInndata inndata, CancellationToken ct = default);
    Task SlettAsync(Guid id, CancellationToken ct = default);
}
