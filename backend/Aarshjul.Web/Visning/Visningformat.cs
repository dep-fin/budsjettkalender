using Aarshjul.Application.Frister;
using Aarshjul.Domain;

namespace Aarshjul.Web.Visning;

/// <summary>Felles formatering for visningene: ærlig dato-/tentativtekst og fargekoding på budsjettår.</summary>
public static class Visningformat
{
    private static readonly string[] Maaneder =
    [
        "januar", "februar", "mars", "april", "mai", "juni",
        "juli", "august", "september", "oktober", "november", "desember"
    ];

    /// <summary>Viser dagspresise frister som dato, tentative som måned (evt. primo/medio/ultimo) — aldri en falsk dag.</summary>
    public static string FormatDato(FristDto f)
    {
        return FormatDato(f.Dato, f.Datopresisjon, f.Datokvalifikator, f.Klokkeslett);
    }

    public static string FormatDato(DateOnly dato, Datopresisjon presisjon, Datokvalifikator? datokvalifikator = null, TimeOnly? klokkeslett = null)
    {
        var maaned = Maaneder[dato.Month - 1];
        if (presisjon == Datopresisjon.Dag)
        {
            var klokke = klokkeslett is { } kl ? $" kl. {kl:HH\\:mm}" : "";
            return $"{dato.Day}. {maaned} {dato.Year}{klokke}";
        }

        var kvalifikator = datokvalifikator switch
        {
            Datokvalifikator.Primo => "primo ",
            Datokvalifikator.Medio => "medio ",
            Datokvalifikator.Ultimo => "ultimo ",
            _ => ""
        };
        return $"{kvalifikator}{maaned} {dato.Year}";
    }

    /// <summary>CSS-klasse for fargekoding på budsjettår, slik at overlappende løp skilles (kravdok. 7).</summary>
    public static string FargeklasseForAar(int budsjettaar) => $"aar-farge-{((budsjettaar % 6) + 6) % 6}";

    public static string KategoriNavn(Kategori kategori) => kategori switch
    {
        Kategori.Budsjett => "Budsjett",
        Kategori.Gulbok => "Gul bok",
        Kategori.Regnskap => "Regnskap",
        _ => kategori.ToString()
    };
}
