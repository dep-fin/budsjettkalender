using System.Text.Json;
using System.Text.Json.Serialization;
using Aarshjul.Domain;

namespace Aarshjul.Application.Generering;

/// <summary>
/// Parametre for <see cref="Regeltype.FastDato"/>: samme måned/dag hvert år, justert til nærmeste
/// virkedag ved helg. <c>aar_forskyvning</c> uttrykker at fristen for budsjettår T faller i
/// kalenderår T + forskyvning (årshjulet spenner ~18 mnd; standard 0 = kalenderår = budsjettår).
/// </summary>
public sealed record FastDatoParametre(
    [property: JsonPropertyName("maaned"), JsonRequired] int Maaned,
    [property: JsonPropertyName("dag"), JsonRequired] int Dag,
    [property: JsonPropertyName("aar_forskyvning")] int AarForskyvning = 0);

/// <summary>
/// Parametre for <see cref="Regeltype.RelativUkedag"/>. To modi:
/// <list type="bullet">
/// <item>n-te forekomst av en ukedag i måneden (<c>uke</c>, 1–5), f.eks. «andre mandag i mars».</item>
/// <item>første forekomst av ukedagen <b>på eller etter</b> <c>fra_dag</c>, f.eks. «mandagen i
/// uken mellom 20. og 26. juli» = første mandag på/etter 20. juli. Når <c>fra_dag</c> er satt,
/// brukes denne i stedet for <c>uke</c>.</item>
/// </list>
/// <c>aar_forskyvning</c> som for <see cref="FastDatoParametre"/>.
/// </summary>
public sealed record RelativUkedagParametre(
    [property: JsonPropertyName("maaned"), JsonRequired] int Maaned,
    [property: JsonPropertyName("uke")] int Uke,
    [property: JsonPropertyName("ukedag"), JsonRequired] string Ukedag,
    [property: JsonPropertyName("aar_forskyvning")] int AarForskyvning = 0,
    [property: JsonPropertyName("fra_dag")] int? FraDag = null);

/// <summary>
/// Parametre for <see cref="Regeltype.RelativTilMilepael"/>: forskyvning i dager fra et anker-løps
/// beregnede dato. Flytter seg automatisk når ankeret flyttes.
/// </summary>
public sealed record RelativTilMilepaelParametre(
    [property: JsonPropertyName("anker_loep"), JsonRequired] string AnkerLoep,
    [property: JsonPropertyName("offset_dager"), JsonRequired] int OffsetDager);

/// <summary>
/// Tolker og validerer <see cref="Gjentaksregel.Regelparametre"/> (JSON) som typede parameter-DTO-er.
/// Manglende felter, ugyldig JSON og ugyldige verdier gir <see cref="FormatException"/>, også ved
/// lesing av eldre lagrede regler. Ukedag-koder («man», «tir», …) mappes til <see cref="DayOfWeek"/>.
/// </summary>
public static class Regelparser
{
    private static readonly JsonSerializerOptions Opsjoner = new(JsonSerializerDefaults.Web);

    /// <summary>Felles parameterkontroll for manuell lagring og import. Kaster <see cref="FormatException"/>.</summary>
    public static void Valider(Regeltype type, string? json)
    {
        switch (type)
        {
            case Regeltype.FastDato:
                FastDato(json);
                break;
            case Regeltype.RelativUkedag:
                RelativUkedag(json);
                break;
            case Regeltype.RelativTilMilepael:
                RelativTilMilepael(json);
                break;
            default:
                throw new FormatException("Ukjent regeltype.");
        }
    }

    public static FastDatoParametre FastDato(string? json)
    {
        var parametre = Deserialiser<FastDatoParametre>(json);
        ValiderMaanedOgDag(parametre.Maaned, parametre.Dag);
        return parametre;
    }

    public static RelativUkedagParametre RelativUkedag(string? json)
    {
        var parametre = Deserialiser<RelativUkedagParametre>(json);
        TolkUkedag(parametre.Ukedag);
        ValiderMaanedOgDag(parametre.Maaned, parametre.FraDag ?? 1);
        if (parametre.FraDag is null && parametre.Uke is < 1 or > 5)
        {
            throw new FormatException("«uke» må være 1–5 når «fra_dag» ikke er satt.");
        }
        return parametre;
    }

    public static RelativTilMilepaelParametre RelativTilMilepael(string? json)
    {
        var parametre = Deserialiser<RelativTilMilepaelParametre>(json);
        if (string.IsNullOrWhiteSpace(parametre.AnkerLoep))
        {
            throw new FormatException("Anker-løp må fylles ut for relativ-til-milepæl-regel.");
        }
        return parametre;
    }

    public static string Serialiser<T>(T parametre)
        => JsonSerializer.Serialize(parametre, Opsjoner);

    /// <summary>Mapper en norsk ukedagkode til <see cref="DayOfWeek"/>. Kaster <see cref="FormatException"/> ved ukjent kode.</summary>
    public static DayOfWeek TolkUkedag(string? kode) => kode?.Trim().ToLowerInvariant() switch
    {
        "man" or "mandag" => DayOfWeek.Monday,
        "tir" or "tirsdag" => DayOfWeek.Tuesday,
        "ons" or "onsdag" => DayOfWeek.Wednesday,
        "tor" or "torsdag" => DayOfWeek.Thursday,
        "fre" or "fredag" => DayOfWeek.Friday,
        "lor" or "lør" or "lørdag" => DayOfWeek.Saturday,
        "son" or "søn" or "søndag" => DayOfWeek.Sunday,
        _ => throw new FormatException($"Ukjent ukedagkode: «{kode}».")
    };

    private static T Deserialiser<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FormatException("Regelparametre må fylles ut.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Opsjoner)
                ?? throw new FormatException("Regelparametre må være et JSON-objekt.");
        }
        catch (JsonException e)
        {
            throw new FormatException("Ugyldig JSON eller manglende/ugyldige parameterfelter.", e);
        }
    }

    private static void ValiderMaanedOgDag(int maaned, int dag)
    {
        if (maaned is < 1 or > 12)
        {
            throw new FormatException("Måned må være 1–12.");
        }
        // Malen gjelder flere år: 29. februar er tillatt, uavhengig av året den lagres i.
        if (dag < 1 || dag > DateTime.DaysInMonth(2000, maaned))
        {
            throw new FormatException("Dagen finnes ikke i den valgte måneden.");
        }
    }
}
