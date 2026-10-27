using System.Globalization;
using Aarshjul.Domain;

namespace Aarshjul.Web.Components.Pages.Admin;

/// <summary>Årsmalens visningsvalg i den aktive Blazor-kretsen, atskilt fra regelutkast.</summary>
public sealed class Mallistekontekst
{
    public string Soek { get; set; } = "";
    public HashSet<Kategori> Kategorier { get; } = [Kategori.Budsjett, Kategori.Gulbok, Kategori.Regnskap];
    public Regelsammenheng Sammenheng { get; set; }
    public Regelsortering Sortering { get; set; }
    public string Aarsfelt { get; set; } = "";
    public int? Budsjettaar { get; set; }
    public Guid? AapnetRegel { get; set; }
    public string? Returfokus { get; set; }
    public Guid? LagretRegel { get; set; }

    public void Initialiser(int aar)
    {
        if (Budsjettaar is not null) return;
        Budsjettaar = aar;
        Aarsfelt = aar.ToString(CultureInfo.InvariantCulture);
    }

    public void NullstillFiltre()
    {
        Soek = "";
        Kategorier.Clear();
        Kategorier.UnionWith([Kategori.Budsjett, Kategori.Gulbok, Kategori.Regnskap]);
        Sammenheng = Regelsammenheng.Alle;
    }
}

public enum Regelsammenheng { Alle, Milepael, Selvstendig, Avhengig }
public enum Regelsortering { Tidligst, Senest, Tittel }
