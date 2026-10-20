using Aarshjul.Application;

namespace Aarshjul.Infrastructure.Datautveksling;

internal static class Importvalidering
{
    public static void Kontroller<T>(int versjon, IReadOnlyList<T>? rader, Func<T, Guid> hentId)
    {
        if (versjon != 1)
            throw new Valideringsfeil($"Ustøttet filversjon: {versjon}. Forventet versjon 1.");
        if (rader is null || rader.Any(r => r is null))
            throw new Valideringsfeil("Importfila må inneholde en liste uten null-rader.");
        var ider = rader.Select(hentId).Where(id => id != Guid.Empty).ToList();
        if (ider.Distinct().Count() != ider.Count)
            throw new Valideringsfeil("Importfila inneholder flere rader med samme ID.");
    }
}
