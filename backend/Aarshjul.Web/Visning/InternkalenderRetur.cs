namespace Aarshjul.Web.Visning;

/// <summary>En engangskvittering ved retur fra et internt skjema i samme krets.</summary>
public sealed class InternkalenderRetur
{
    private (string Sti, string Tekst)? _ventende;
    public void Lagre(string sti, string tekst) => _ventende = (sti, tekst);
    public string? Hent(string sti)
    {
        if (_ventende is not { } melding || melding.Sti != sti) return null;
        _ventende = null;
        return melding.Tekst;
    }
}
