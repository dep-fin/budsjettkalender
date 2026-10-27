using System.Globalization;
using Aarshjul.Domain;
using Microsoft.AspNetCore.WebUtilities;

namespace Aarshjul.Web.Visning;

/// <summary>Administratorens arbeidsår følger lenken, uavhengig av leserens kalenderutvalg.</summary>
public static class Arbeidsaarkontekst
{
    public static int Standardaar(TimeProvider klokke) => Math.Min(9999, Datohjelp.Idag(klokke).Year + 1);

    public static int? Les(string? verdi, TimeProvider klokke) => verdi == "alle" ? null
        : int.TryParse(verdi, NumberStyles.None, CultureInfo.InvariantCulture, out var aar) && aar is >= 1 and <= 9999
            ? aar : Standardaar(klokke);

    public static int? FraUri(string uri, TimeProvider klokke)
    {
        var query = QueryHelpers.ParseQuery(new Uri(uri).Query);
        return Les(query.TryGetValue("aar", out var aar) ? aar.ToString() : null, klokke);
    }

    public static string Lenke(string sti, int? aar) => QueryHelpers.AddQueryString(sti, "aar",
        aar?.ToString(CultureInfo.InvariantCulture) ?? "alle");
}
