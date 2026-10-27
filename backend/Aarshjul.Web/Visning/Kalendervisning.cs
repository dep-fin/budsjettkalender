using System.Globalization;
using Aarshjul.Application.Frister;

namespace Aarshjul.Web.Visning;

/// <summary>Månedsruter og norsk datotekst. Utfyllingsruter utenfor DateOnly er tomme.</summary>
public static class Kalendervisning
{
    private static readonly CultureInfo Norsk = CultureInfo.GetCultureInfo("nb-NO");

    public static string Dato(DateOnly dato) => dato.ToString("d. MMMM yyyy", Norsk);
    public static string Maaned(DateOnly dato) => dato.ToString("MMMM yyyy", Norsk);
    public static DateOnly FoersteDag(DateOnly dato) => new(dato.Year, dato.Month, 1);
    public static DateOnly SisteDag(DateOnly dato) => new(dato.Year, dato.Month, DateTime.DaysInMonth(dato.Year, dato.Month));
    public static bool KanFlytte(DateOnly dato, int steg) => steg < 0
        ? dato.Year > 1 || dato.Month > 1
        : dato.Year < 9999 || dato.Month < 12;

    public static IReadOnlyList<DateOnly?[]> Uker(DateOnly maaned)
    {
        var foerste = FoersteDag(maaned);
        var forskyvning = ((int)foerste.DayOfWeek + 6) % 7;
        var antall = (forskyvning + DateTime.DaysInMonth(maaned.Year, maaned.Month) + 6) / 7;
        var uker = new List<DateOnly?[]>();
        for (var uke = 0; uke < antall; uke++)
        {
            var dager = new DateOnly?[7];
            for (var dag = 0; dag < 7; dag++)
            {
                var dagnummer = foerste.DayNumber - forskyvning + uke * 7 + dag;
                if (dagnummer >= DateOnly.MinValue.DayNumber && dagnummer <= DateOnly.MaxValue.DayNumber)
                    dager[dag] = DateOnly.FromDayNumber(dagnummer);
            }
            uker.Add(dager);
        }
        return uker;
    }

    public static (int Aar, int Nummer) Isouke(DateOnly?[] uke)
    {
        var dato = uke.First(d => d.HasValue)!.Value.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(dato), ISOWeek.GetWeekOfYear(dato));
    }

    public static IReadOnlyList<FristDto> Dagsfrister(IEnumerable<FristDto> frister, DateOnly dato)
        => frister.Where(f => !f.ErTentativ && f.Dato == dato)
            .OrderBy(f => f.Klokkeslett).ThenBy(f => f.Tittel, StringComparer.CurrentCulture).ThenBy(f => f.Id).ToArray();
}
