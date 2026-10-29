using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Domain;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Skjuler lagringsformat og historiske utvalgsgrenser. Null er wildcard; internt tomt snitt fjernes.</summary>
internal sealed record Abonnementsutvalg(int[]? Budsjettaar, Kategori[]? Kategorier, Utvalgsgrense[] Grenser)
{
    internal static Abonnementsutvalg Les(string? json) => json is null
        ? new(null, null, [new(null, null, 0)]) : JsonSerializer.Deserialize<Abonnementsutvalg>(json)!;
    internal string Json() => JsonSerializer.Serialize(this);

    internal Abonnementsutvalg MedValg(IReadOnlyCollection<int>? aar, IReadOnlyCollection<Kategori>? kategorier)
    {
        if (aar?.Any(a => a is < 1 or > 9999) == true) throw new Valideringsfeil("Velg gyldige budsjettår mellom 1 og 9999.");
        if (kategorier?.Any(k => !Enum.IsDefined(k)) == true) throw new Valideringsfeil("Velg gyldige kategorier.");
        return new(Normaliser(aar), Normaliser(kategorier), Grenser);
    }

    internal Abonnementsutvalg Aktiver(Abonnementsutvalg tidligere, long sekvens, bool nyAktivering)
    {
        var kandidater = new List<Utvalgsgrense>();
        if (!nyAktivering)
        {
            foreach (var grense in tidligere.Grenser)
            {
                var aar = Snitt(grense.Budsjettaar, Budsjettaar);
                var kategorier = Snitt(grense.Kategorier, Kategorier);
                if (aar is { Length: 0 } || kategorier is { Length: 0 }) continue;
                kandidater.Add(new(aar, kategorier, grense.Sekvens));
            }
        }
        kandidater.Add(new(Budsjettaar, Kategorier, sekvens));
        var beholdt = new List<Utvalgsgrense>();
        foreach (var grense in kandidater.OrderBy(g => g.Sekvens))
            if (!beholdt.Any(g => Dekker(g.Budsjettaar, grense.Budsjettaar) && Dekker(g.Kategorier, grense.Kategorier)))
                beholdt.Add(grense);
        return this with { Grenser = beholdt.ToArray() };
    }

    internal bool Treffer(long sekvens, int aar, Kategori kategori) => Grenser.Any(g => sekvens > g.Sekvens
        && (g.Budsjettaar is null || g.Budsjettaar.Contains(aar)) && (g.Kategorier is null || g.Kategorier.Contains(kategori)));

    private static T[]? Normaliser<T>(IReadOnlyCollection<T>? verdier) => verdier is null || verdier.Count == 0
        ? null : verdier.Distinct().Order().ToArray();
    private static T[]? Snitt<T>(T[]? a, T[]? b) => a is null ? b : b is null ? a : a.Intersect(b).ToArray();
    private static bool Dekker<T>(T[]? a, T[]? b) => a is null || b is not null && b.All(a.Contains);
}

internal sealed record Utvalgsgrense(int[]? Budsjettaar, Kategori[]? Kategorier, long Sekvens);
