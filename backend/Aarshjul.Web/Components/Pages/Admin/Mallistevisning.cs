using System.Globalization;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;

namespace Aarshjul.Web.Components.Pages.Admin;

/// <summary>Presentasjon over et ferdig beregnet, ufiltrert snapshot. Endrer aldri ankerrekkefølgen.</summary>
internal static class Mallistevisning
{
    private static readonly CultureInfo Norsk = CultureInfo.GetCultureInfo("nb-NO");
    private static readonly StringComparer Tittelrekkefolge = StringComparer.Create(Norsk, true);

    internal sealed record Rad(MalRegelDto Regel, MalRegelForhaandsvisning Beregning,
        Regelsammenheng Sammenheng, string? Anker, IReadOnlyList<MalRegelDto> Barn)
    {
        public string Tittel => Navn(Regel);
        public string Setning => Malregelutkast.Beskriv(Regel);
        public bool ErFeil => Beregning.Feil is not null || Beregning.Dato is null;
        public string Sammenhengstekst => Sammenheng switch
        {
            Regelsammenheng.Milepael => "Milepæl",
            Regelsammenheng.Avhengig => "Avhengig",
            _ => "Selvstendig"
        };
        public DateOnly? Sorteringsdato => Beregning.Dato is { } dato
            ? Datoberegning.Sorteringsdag(dato, Beregning.Presisjon, null) : null;
    }

    public static IReadOnlyList<Rad> Lag(MalForhaandsvisning snapshot)
    {
        var representanter = new Dictionary<string, MalRegelDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var regel in snapshot.Regler) representanter.TryAdd(regel.Loep, regel);
        var barn = new Dictionary<Guid, List<MalRegelDto>>();
        var ankre = new Dictionary<Guid, string>();
        foreach (var regel in snapshot.Regler.Where(r => r.Regeltype == Regeltype.RelativTilMilepael))
        {
            try
            {
                var anker = Regelparser.RelativTilMilepael(regel.Regelparametre).AnkerLoep;
                ankre[regel.Id] = anker;
                if (!representanter.TryGetValue(anker, out var representant)) continue;
                if (!barn.TryGetValue(representant.Id, out var koblinger)) barn[representant.Id] = koblinger = [];
                koblinger.Add(regel);
            }
            catch (FormatException) { /* Uleselige relative regler er avhengige uten en oppdiktet kant. */ }
        }
        return snapshot.Regler.Select((regel, i) => new Rad(regel, snapshot.Beregninger[i],
            regel.Regeltype == Regeltype.RelativTilMilepael ? Regelsammenheng.Avhengig
                : barn.ContainsKey(regel.Id) ? Regelsammenheng.Milepael : Regelsammenheng.Selvstendig,
            ankre.GetValueOrDefault(regel.Id), barn.TryGetValue(regel.Id, out var koblinger) ? koblinger : [])).ToList();
    }

    public static IReadOnlyList<Rad> Velg(IReadOnlyList<Rad> rader, Mallistekontekst valg)
    {
        var soek = valg.Soek.Trim();
        var utvalg = rader.Where(r => valg.Kategorier.Contains(r.Regel.Kategori)
            && (valg.Sammenheng == Regelsammenheng.Alle || valg.Sammenheng == r.Sammenheng)
            && (soek.Length == 0 || Treffer(r.Tittel, soek) || Treffer(r.Regel.Loep, soek) || Treffer(r.Setning, soek)));
        IOrderedEnumerable<Rad> sortert = valg.Sortering switch
        {
            Regelsortering.Tittel => utvalg.OrderBy(r => r.Tittel, Tittelrekkefolge),
            Regelsortering.Senest => utvalg.OrderBy(r => r.ErFeil).ThenByDescending(r => r.ErFeil ? null : r.Sorteringsdato)
                .ThenBy(r => r.Tittel, Tittelrekkefolge),
            _ => utvalg.OrderBy(r => r.ErFeil).ThenBy(r => r.ErFeil ? null : r.Sorteringsdato)
                .ThenBy(r => r.Tittel, Tittelrekkefolge)
        };
        return sortert.ThenBy(r => r.Regel.Id.ToString("D"), StringComparer.Ordinal).ToList();
    }

    private static bool Treffer(string tekst, string soek) => Norsk.CompareInfo.IndexOf(tekst, soek, CompareOptions.IgnoreCase) >= 0;
    public static string Navn(MalRegelDto regel) => string.IsNullOrWhiteSpace(regel.Tittel) ? regel.Loep : regel.Tittel;
    public static string Dato(MalRegelForhaandsvisning rad) => rad.Dato is not { } dato ? "Ingen dato"
        : rad.Presisjon == Datopresisjon.Maaned ? $"{dato.ToString("MMMM yyyy", Norsk)} · tentativ"
        : $"{dato.ToString("d. MMMM yyyy", Norsk)}{(rad.Tentativ ? " · tentativ" : "")}";
    public static string? Maskindato(MalRegelForhaandsvisning rad) => rad.Dato?.ToString(
        rad.Presisjon == Datopresisjon.Maaned ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
