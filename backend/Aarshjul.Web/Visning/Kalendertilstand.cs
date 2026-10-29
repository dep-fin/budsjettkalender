using Aarshjul.Domain;
using Aarshjul.Application.Frister;

namespace Aarshjul.Web.Visning;

public enum Kalenderpresentasjon { Maaned, Uke, Liste }
public enum Kalenderperiode { Maaned, Uke }

/// <summary>
/// Kalenderens periode og eksplisitte utvalg i samme Blazor-krets. Delt av kalenderens
/// presentasjoner, atskilt fra Fristlistens null/tom=alle og historikkvalg.
/// </summary>
public sealed class Kalendertilstand
{
    public DateOnly? ValgtDato { get; set; }
    public Kalenderpresentasjon Visning { get; set; } = Kalenderpresentasjon.Maaned;
    // Kalenderperioden bevares mens Liste viser sitt eget tidsutvalg.
    public Kalenderperiode Periode { get; set; } = Kalenderperiode.Maaned;
    public const int Listesidestoerrelse = 25;
    public bool VisListehistorikk { get; set; }
    private readonly Listespor _kommende = new();
    private readonly Listespor _historikk = new();
    private Listespor AktivListe => VisListehistorikk ? _historikk : _kommende;
    public int Listeside => AktivListe.Side;
    public bool ListeOppdatert { get; private set; }
    public DateOnly? Listedato => AktivListe.Dato;
    public int AntallListefrister { get; private set; }
    public int AntallListesider => (AntallListefrister + Listesidestoerrelse - 1) / Listesidestoerrelse;
    public HashSet<Kategori> Kategorier { get; } = [Kategori.Budsjett, Kategori.Gulbok];
    public HashSet<int> Budsjettaar { get; } = [];
    public string Soek { get; set; } = "";
    public FristDto? OverfoertFrist { get; set; }
    private bool _aarsvalgInitialisert;

    public IReadOnlyList<FristDto> HentListeside(IReadOnlyList<FristDto> frister, DateOnly idag, int sidesteg)
    {
        var utvalg = frister.Where(f => (f.ErTentativ
                ? Kalendervisning.SisteDag(f.Dato) < idag : f.Dato < idag) == VisListehistorikk);
        var datoordnet = VisListehistorikk ? utvalg.OrderByDescending(f => f.Sorteringsdag) : utvalg.OrderBy(f => f.Sorteringsdag);
        var tidsordnet = VisListehistorikk
            ? datoordnet.ThenBy(f => f.HarKlokkeslett).ThenByDescending(f => f.Klokkeslett)
            : datoordnet.ThenBy(f => f.Klokkeslett);
        var ordnet = tidsordnet
            .ThenBy(f => f.Tittel, StringComparer.CurrentCulture).ThenBy(f => f.Id).ToArray();
        var ider = ordnet.Select(f => f.Id).ToArray();
        var spor = AktivListe;
        ListeOppdatert = spor.Ider is not null && (spor.Dato != idag || !spor.Ider.SequenceEqual(ider));
        spor.Ider = ider;
        spor.Dato = idag;
        AntallListefrister = ordnet.Length;
        spor.Side = ListeOppdatert ? 1 : Math.Clamp(spor.Side + sidesteg, 1, Math.Max(1, AntallListesider));
        return ordnet.Skip((Listeside - 1) * Listesidestoerrelse).Take(Listesidestoerrelse).ToArray();
    }

    public void NullstillListesider()
    {
        foreach (var spor in new[] { _kommende, _historikk })
        {
            spor.Side = 1;
            spor.Ider = null;
            spor.Dato = null;
        }
        ListeOppdatert = false;
    }

    private sealed class Listespor
    {
        public int Side = 1;
        public Guid[]? Ider;
        public DateOnly? Dato;
    }

    public void InitialiserAar(IReadOnlyList<int> tilgjengeligeAar)
    {
        if (_aarsvalgInitialisert) return;
        Budsjettaar.UnionWith(tilgjengeligeAar);
        _aarsvalgInitialisert = true;
    }

    public void Nullstill(IReadOnlyList<int> tilgjengeligeAar)
    {
        Kategorier.Clear();
        Kategorier.UnionWith([Kategori.Budsjett, Kategori.Gulbok]);
        Budsjettaar.Clear();
        Budsjettaar.UnionWith(tilgjengeligeAar);
        Soek = "";
        _aarsvalgInitialisert = true;
    }
}
