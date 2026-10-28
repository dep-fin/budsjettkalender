using System.Globalization;

namespace Aarshjul.Web.Components.Pages.Admin;

/// <summary>Midlertidige prøvedatoer og uttrykkelig overgang til generering i én Blazor-krets.</summary>
public sealed class Malproevekontekst
{
    private readonly Dictionary<int, Dictionary<string, DateOnly>> _proeveankre = [];
    private readonly Dictionary<int, Dictionary<string, DateOnly>> _genereringsankre = [];
    private int? _overfoertAar;
    public int? Budsjettaar { get; set; }
    public string Aarsfelt { get; set; } = "";
    public Guid? Returregel { get; set; }

    public void Initialiser(int aar)
    {
        if (Budsjettaar is not null) return;
        Budsjettaar = aar;
        Aarsfelt = aar.ToString(CultureInfo.InvariantCulture);
    }

    public Dictionary<string, DateOnly> Proeveankre(int aar) => Hent(_proeveankre, aar);
    public Dictionary<string, DateOnly> Genereringsankre(int aar) => Hent(_genereringsankre, aar);

    public void ForberedGenerering(int aar)
    {
        _genereringsankre[aar] = new(Proeveankre(aar), StringComparer.OrdinalIgnoreCase);
        _overfoertAar = aar;
    }

    public bool TaImotOvergang(int aar)
    {
        if (_overfoertAar != aar) return false;
        _overfoertAar = null;
        return true;
    }

    private static Dictionary<string, DateOnly> Hent(Dictionary<int, Dictionary<string, DateOnly>> alle, int aar)
    {
        if (!alle.TryGetValue(aar, out var datoer)) alle[aar] = datoer = new(StringComparer.OrdinalIgnoreCase);
        return datoer;
    }
}
