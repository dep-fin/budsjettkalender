using Aarshjul.Application.Frister;
using Aarshjul.Application.Faser;

namespace Aarshjul.Web.Visning;

/// <summary>Presentasjonsvindu og kollisjonsfrie månedsgrupper. Avgrenser aldri leseutvalget.</summary>
public sealed class Budsjettaarslinje
{
    public IReadOnlyList<(string Navn, int Fra, int Til)> Faser { get; }
    public DateOnly Fra { get; }
    public DateOnly Til { get; }
    public IReadOnlyList<FristDto> Foer { get; }
    public IReadOnlyList<FristDto> Etter { get; }
    public IReadOnlyList<Stasjon> Stasjoner { get; }
    public int Spor { get; }
    private readonly long _start;
    private readonly long _slutt;
    private readonly long _budsjettstart;

    public Budsjettaarslinje(int budsjettaar, IReadOnlyList<FristDto> frister, FasemalDto? fasemal = null)
    {
        fasemal ??= Fasemalstandard.Standard;
        Faser = fasemal.Faser.Select((fase, i) => (fase.Navn, fase.Startmaaned,
            i + 1 < fasemal.Faser.Count ? fasemal.Faser[i + 1].Startmaaned : checked(fasemal.SisteMaanedInkludert + 1))).ToArray();
        _budsjettstart = ((long)budsjettaar - 1) * 12;
        // Eksisterende årstall har ingen DateOnly-kontrakt. Regn med long og klipp bare grafikken.
        _start = Math.Clamp(_budsjettstart + Faser[0].Fra, 0, 9999L * 12 - 1);
        _slutt = Math.Clamp(_budsjettstart + Faser[^1].Til, _start + 1, 9999L * 12);
        Fra = FraMaaned(_start);
        Til = Kalendervisning.SisteDag(FraMaaned(_slutt - 1));
        Foer = frister.Where(f => f.Dato < Fra).ToArray();
        Etter = frister.Where(f => f.Dato > Til).ToArray();
        var stasjoner = new List<Stasjon>();
        var sistePosisjon = new List<double>();
        foreach (var gruppe in frister.Where(f => f.Dato >= Fra && f.Dato <= Til)
                     .GroupBy(f => (Maaned: Kalendervisning.FoersteDag(f.Dato), f.ErTentativ))
                     .OrderBy(g => g.Key.Maaned).ThenBy(g => g.Key.ErTentativ))
        {
            var innhold = gruppe.OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Klokkeslett).ThenBy(f => f.Tittel).ThenBy(f => f.Id).ToArray();
            var posisjon = innhold.Length == 1 && !gruppe.Key.ErTentativ ? Posisjon(innhold[0].Dato) : Posisjon(gruppe.Key.Maaned, midtIMaaned: true);
            // Kortene er 164 px på en flate på minst 1080 px. Reserver en hel kortbredde per spor.
            // Bruk den klippede kortkanten, også ved aksens første/siste måned.
            var kortkant = Math.Clamp(posisjon - 8, 0, 84);
            var spor = sistePosisjon.FindIndex(p => kortkant - p >= 17);
            if (spor < 0) { spor = sistePosisjon.Count; sistePosisjon.Add(kortkant); }
            else sistePosisjon[spor] = kortkant;
            stasjoner.Add(new(gruppe.Key.Maaned, gruppe.Key.ErTentativ, innhold, posisjon, spor));
        }
        Stasjoner = stasjoner;
        Spor = Math.Max(2, sistePosisjon.Count);
    }

    public record Stasjon(DateOnly Maaned, bool Tentativ, IReadOnlyList<FristDto> Frister, double Posisjon, int Spor)
    {
        public string Id => $"maanedsgruppe-{Maaned:yyyy-MM}-{(Tentativ ? "tentativ" : "fast")}";
    }

    public IEnumerable<(int Aar, int Maaneder)> Aarbaand()
    {
        for (var maaned = _start; maaned < _slutt;)
        {
            var neste = Math.Min(_slutt, (maaned / 12 + 1) * 12);
            yield return ((int)(maaned / 12 + 1), (int)(neste - maaned));
            maaned = neste;
        }
    }

    public IEnumerable<DateOnly> Maaneder()
    {
        for (var maaned = _start; maaned < _slutt; maaned++) yield return FraMaaned(maaned);
    }

    public double Posisjon(DateOnly dato, bool midtIMaaned = false)
        => (Maanedstall(dato) - _start + (midtIMaaned ? .5 : (dato.Day - 1d) / DateTime.DaysInMonth(dato.Year, dato.Month))) / (_slutt - _start) * 100;

    public (double Venstre, double Bredde) Fase(int fra, int til)
    {
        var start = Math.Clamp(_budsjettstart + fra, _start, _slutt);
        var slutt = Math.Clamp(_budsjettstart + til, _start, _slutt);
        return ((start - _start) * 100d / (_slutt - _start), (slutt - start) * 100d / (_slutt - _start));
    }

    public static string RelativtAar(int kalenderaar, int budsjettaar)
    {
        var forskjell = (long)kalenderaar - budsjettaar;
        return forskjell == 0 ? "t" : forskjell < 0 ? $"t−{-forskjell}" : $"t+{forskjell}";
    }

    private static long Maanedstall(DateOnly dato) => ((long)dato.Year - 1) * 12 + dato.Month - 1;
    private static DateOnly FraMaaned(long maaned) => new((int)(maaned / 12 + 1), (int)(maaned % 12 + 1), 1);
}
