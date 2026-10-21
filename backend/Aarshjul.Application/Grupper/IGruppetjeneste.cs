namespace Aarshjul.Application.Grupper;

/// <summary>Leser synlighetsgrupper for synlighetsvalg og administrasjon (kravdok. 2.3, 3.6).</summary>
public interface IGruppetjeneste
{
    /// <summary>Aktive grupper som kan velges i synlighetsvalg, sortert med standardgrupper først.</summary>
    Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default);

    /// <summary>Alle grupper, også inaktive, for administratorens forvaltning.</summary>
    Task<IReadOnlyList<GruppeadministrasjonDto>> HentAlleAsync(CancellationToken ct = default);

    /// <summary>
    /// Oppretter en aktiv egendefinert gruppe. Kode: 1–64 ASCII-bokstaver, sifre eller bindestrek,
    /// med bokstav først; trimmes og lagres med store bokstaver. Navn: 1–256 tegn etter trimming.
    /// Duplikater avvises også for inaktive grupper.
    /// </summary>
    Task OpprettAsync(string kode, string navn, CancellationToken ct = default);

    /// <summary>
    /// Endrer navn og status. Koden identifiserer gruppen og kan aldri endres.
    /// Bevarer innebygd identitet og alle eksisterende referanser. Ingen sletteoperasjon tilbys.
    /// </summary>
    Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default);
}

public record GruppeDto(string Kode, string Navn, bool ErStandard);
public record GruppeadministrasjonDto(string Kode, string Navn, bool Aktiv, bool ErStandard);
