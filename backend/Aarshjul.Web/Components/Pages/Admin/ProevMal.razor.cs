using System.Globalization;
using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Aarshjul.Web.Visning;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class ProevMal : IAsyncDisposable
{
    [Inject] private IGenereringstjeneste Generering { get; set; } = default!;
    [Inject] private Malproevekontekst Kontekst { get; set; } = default!;
    [Inject] private TimeProvider Klokke { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private ILogger<ProevMal> Logg { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "aar")] public string? AarQuery { get; set; }
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<MalRegelDto> _regler = [];
    private IReadOnlyList<Mallistevisning.Rad> _rader = [];
    private int _versjon, _vistAar;
    private bool _laster, _lastet, _avsluttet, _fokuserRetur;
    private string? _feil, _status, _parameternoekkel;
    private Dictionary<string, DateOnly> Ankre => Kontekst.Proeveankre(Kontekst.Budsjettaar!.Value);
    private bool ProevAar(out int aar) => int.TryParse(Kontekst.Aarsfelt, NumberStyles.None, CultureInfo.InvariantCulture, out aar) && aar is >= 1 and <= 9999;
    private string? Aarsfeil => ProevAar(out _) ? null : "Budsjettåret må være et heltall fra 1 til 9999. Siste gyldige resultat beholder sin årsetikett.";

    protected override async Task OnParametersSetAsync()
    {
        var noekkel = AarQuery ?? "";
        if (_parameternoekkel == noekkel) return;
        _parameternoekkel = noekkel;
        Kontekst.Initialiser(Datohjelp.Idag(Klokke).Year);
        if (AarQuery is not null)
        {
            Kontekst.Aarsfelt = AarQuery;
            if (ProevAar(out var aar)) Kontekst.Budsjettaar = aar;
            else { ++_versjon; _laster = false; return; }
        }
        _fokuserRetur = Kontekst.Returregel is not null;
        if (_fokuserRetur) _status = "Den lagrede malen er lest på nytt. Prøveår og midlertidige datoer er beholdt.";
        Kontekst.Returregel = null;
        await LastAsync();
    }

    private async Task LastAsync()
    {
        if (!ProevAar(out var aar)) return;
        var versjon = ++_versjon;
        _laster = true;
        _feil = null;
        var ankre = new Dictionary<string, DateOnly>(Kontekst.Proeveankre(aar), StringComparer.OrdinalIgnoreCase);
        try
        {
            var snapshot = await Generering.ProevAsync(new(aar, ankre), _levetid.Token);
            if (_avsluttet || versjon != _versjon) return;
            _regler = snapshot.Regler;
            _rader = Mallistevisning.Lag(snapshot);
            _vistAar = snapshot.Budsjettaar;
            _lastet = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (!_avsluttet && versjon == _versjon) _feil = e.Message; }
        catch (Exception e)
        {
            if (_avsluttet || versjon != _versjon) return;
            Logg.LogError(e, "Kunne ikke prøve årsmalen.");
            _feil = "Kunne ikke beregne prøven akkurat nå. Ingen forslag er opprettet. Datoene dine er beholdt; prøv igjen.";
        }
        finally { if (!_avsluttet && versjon == _versjon) _laster = false; }
    }

    private async Task AarEndretAsync(ChangeEventArgs e)
    {
        Kontekst.Aarsfelt = e.Value?.ToString() ?? "";
        if (!ProevAar(out var aar)) { ++_versjon; _laster = false; return; }
        Kontekst.Budsjettaar = aar;
        await LastAsync();
    }

    private async Task DatoEndretAsync((string Loep, DateOnly? Dato) endring)
    {
        if (Aarsfeil is not null) return;
        if (endring.Dato is { } dato) Ankre[endring.Loep] = dato;
        else Ankre.Remove(endring.Loep);
        await LastAsync();
    }

    private async Task FjernAlleAsync() { Ankre.Clear(); await LastAsync(); }
    private void TilGenerering()
    {
        if (_laster || _feil is not null || !ProevAar(out var aar)) return;
        Kontekst.ForberedGenerering(aar);
        Nav.NavigateTo(Arbeidsaarkontekst.Lenke("/admin/generer", aar));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_fokuserRetur || _laster || !_lastet) return;
        _fokuserRetur = false;
        try
        {
            await using var modul = await Js.InvokeAsync<IJSObjectReference>("import", "./regelbygger.js");
            if (!_avsluttet) await modul.InvokeVoidAsync("fokuser", "proeve-tittel");
        }
        catch (JSDisconnectedException) { }
    }

    public ValueTask DisposeAsync() { _avsluttet = true; _levetid.Cancel(); _levetid.Dispose(); return ValueTask.CompletedTask; }
}
