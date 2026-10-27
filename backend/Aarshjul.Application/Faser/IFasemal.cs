namespace Aarshjul.Application.Faser;

/// <summary>Fasens navn og første måned, relativt til januar i budsjettåret t.</summary>
public sealed record FaseDto(string Navn, int Startmaaned);

/// <summary>Én felles mal for alle budsjettår. Siste måned er inkludert.</summary>
public sealed record FasemalDto(Guid Versjon, IReadOnlyList<FaseDto> Faser, int SisteMaanedInkludert);

/// <summary>
/// Leser og erstatter den felles fasemalen samlet. Web-laget autoriserer oppdateringer.
/// Forventet versjon skal være versjonen brukeren opprinnelig leste.
/// </summary>
public interface IFasemal
{
    Task<FasemalDto> HentAsync(CancellationToken ct = default);
    Task<FasemalDto> OppdaterAsync(Guid forventetVersjon, IReadOnlyList<FaseDto> faser,
        int sisteMaanedInkludert, CancellationToken ct = default);
}

/// <summary>Avklart utgangspunkt fra #75; brukes også ved oppretting av databasen.</summary>
public static class Fasemalstandard
{
    public static FasemalDto Standard { get; } = new(
        new Guid("2467c0d7-5575-45ba-b658-79293feff364"),
        Array.AsReadOnly<FaseDto>([
            new("Forberedelser", -15),
            new("Utarbeidelse av budsjettforslag", -10),
            new("Fremleggelse og vedtak", -3),
            new("Gjennomføring og revidering", 0),
            new("Regnskap", 12)
        ]),
        15);
}

public sealed class FasemalValideringsunntak(IReadOnlyDictionary<string, string> feil)
    : Exception(string.Join(" ", feil.Values))
{
    public IReadOnlyDictionary<string, string> Feil { get; } = feil;
}

public sealed class FasemalKonfliktunntak() : Exception(
    "En annen administrator har lagret en nyere fasemal. Last inn den nye malen før du lagrer igjen.");
