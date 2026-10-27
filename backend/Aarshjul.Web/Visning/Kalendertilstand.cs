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
    // Liste følger den sist valgte måned-/ukeperioden, også ved retur til Kalender.
    public Kalenderperiode Periode { get; set; } = Kalenderperiode.Maaned;
    public HashSet<Kategori> Kategorier { get; } = [Kategori.Budsjett, Kategori.Gulbok];
    public HashSet<int> Budsjettaar { get; } = [];
    public string Soek { get; set; } = "";
    public FristDto? OverfoertFrist { get; set; }
    private bool _aarsvalgInitialisert;

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
