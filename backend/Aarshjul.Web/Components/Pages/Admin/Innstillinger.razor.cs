using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Components;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Innstillinger : IDisposable
{
    [Inject] private AdministratoroversiktKommando Kommando { get; set; } = default!;
    [Inject] private ILogger<Innstillinger> Logg { get; set; } = default!;
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<Administratorrad> _administratorer = [];
    private bool _laster = true, _avsluttet;
    private string? _feil;

    protected override Task OnInitializedAsync() => LastAsync();

    private async Task LastAsync()
    {
        _laster = true; _feil = null; _administratorer = [];
        try
        {
            var administratorer = await Kommando.HentAsync(_levetid.Token);
            if (!_avsluttet) _administratorer = administratorer;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (UnauthorizedAccessException)
        {
            if (!_avsluttet) _feil = "Du har ikke tilgang til administratoroversikten.";
        }
        catch (Exception)
        {
            if (_avsluttet) return;
            Logg.LogWarning("Kunne ikke laste administratoroversikten.");
            _feil = "Kunne ikke laste administratorene akkurat nå. Velg Oppdater oversikt for å prøve igjen.";
        }
        finally { if (!_avsluttet) _laster = false; }
    }

    public void Dispose()
    {
        _avsluttet = true; _levetid.Cancel(); _levetid.Dispose();
    }
}
