using Aarshjul.Application.Godkjenningsko;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Aarshjul.Web.Visning;

/// <summary>Engangsretur til køens utvalg etter justering i samme Blazor-krets.</summary>
public sealed class KoJusteringsretur : IDisposable
{
    private readonly NavigationManager _nav;
    private Kofilter? _filter;
    private string? _justeringssti;

    public KoJusteringsretur(NavigationManager nav)
    {
        _nav = nav;
        _nav.LocationChanged += VedNavigasjon;
    }

    public void Bevar(Guid forslagId, Kofilter filter)
    {
        _justeringssti = $"admin/frist/juster/{forslagId}";
        _filter = filter;
    }

    public Kofilter? Hent()
    {
        var filter = _filter;
        _filter = null;
        return filter;
    }

    private void VedNavigasjon(object? sender, LocationChangedEventArgs e)
    {
        var sti = _nav.ToBaseRelativePath(e.Location);
        // Vanlig navigasjon forkaster returen. Ingen URL-, nettleser- eller databaselagring.
        if (sti != _justeringssti && sti != "admin/ko") _filter = null;
    }

    public void Dispose() => _nav.LocationChanged -= VedNavigasjon;
}
