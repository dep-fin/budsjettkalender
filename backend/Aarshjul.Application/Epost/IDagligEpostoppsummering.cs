namespace Aarshjul.Application.Epost;

/// <summary>Planlegger og overleverer dagens oppsummeringer uten en innlogget jobbbruker.</summary>
public interface IDagligEpostoppsummering
{
    Task<Oppsummeringsresultat> KjorAsync(CancellationToken ct = default);
}

public sealed record Oppsummeringsresultat(DateOnly LokalDato, bool FoerSendetid,
    IReadOnlyList<Mottakerresultat> Mottakere);
public sealed record Mottakerresultat(string BrukerId, Guid? OppsummeringId, string Status, int AntallFrister = 0);
