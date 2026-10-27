namespace Aarshjul.Application.Faser;

/// <summary>Validerer hele erstatningen og kopierer normaliserte navn før lagringsarbeidet starter.</summary>
public static class Fasemalvalidering
{
    public static IReadOnlyList<FaseDto> Valider(IReadOnlyList<FaseDto> faser, int sisteMaanedInkludert)
    {
        var feil = new Dictionary<string, string>();
        if (faser is null || faser.Count != 5)
            throw new FasemalValideringsunntak(new Dictionary<string, string>
            {
                ["Faser"] = "Fasemalen må inneholde nøyaktig fem faser i fast rekkefølge."
            });

        var normaliserte = new FaseDto[5];
        for (var i = 0; i < faser.Count; i++)
        {
            var fase = faser[i];
            if (fase is null)
            {
                feil[$"Faser[{i}].Navn"] = $"Fase {i + 1} må ha et navn.";
                continue;
            }

            if (string.IsNullOrWhiteSpace(fase.Navn))
                feil[$"Faser[{i}].Navn"] = $"Fase {i + 1} må ha et navn.";
            normaliserte[i] = new(fase.Navn?.Trim() ?? "", fase.Startmaaned);
            if (i > 0 && faser[i - 1] is { } forrige && fase.Startmaaned <= forrige.Startmaaned)
                feil[$"Faser[{i}].Startmaaned"] = $"Fase {i + 1} må starte etter fase {i}.";
        }

        if (faser[^1] is { } siste && sisteMaanedInkludert < siste.Startmaaned)
            feil["SisteMaanedInkludert"] = "Siste inkluderte måned må være samme måned som eller etter starten på siste fase.";
        try
        {
            // Visningen bruker eksklusiv slutt; én måned i siste fase er gyldig.
            _ = checked(sisteMaanedInkludert + 1);
        }
        catch (OverflowException)
        {
            feil["SisteMaanedInkludert"] = "Siste inkluderte måned er for stor. Velg en gyldig sluttgrense.";
        }

        if (feil.Count > 0) throw new FasemalValideringsunntak(feil);
        return Array.AsReadOnly(normaliserte);
    }
}
