using Aarshjul.Application.Grupper;
using Aarshjul.Application.Utskrift;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Eksport : IAsyncDisposable
{
    [Inject] private IGruppetjeneste Grupper { get; set; } = default!;
    [Inject] private EksportKommando Kommando { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<GruppeDto> _grupper = [];
    private Eksportutvalg? _preview;
    private bool _alt, _laster, _kontrollerer, _avsluttet;
    private string _valgtKode = "";
    private DateOnly? _fra, _til;
    private string? _feil, _status, _fokus;
    private int _valg;
    private bool UgyldigPeriode => _fra is { } fra && _til is { } til && fra > til;
    private bool KanLasteNed => !_laster && !UgyldigPeriode && (_alt || _grupper.Any(g => g.Kode == _valgtKode));
    private bool Gjeldende(int valg) => !_avsluttet && valg == _valg;

    protected override Task OnInitializedAsync() => LastGrupperAsync();

    private async Task LastGrupperAsync()
    {
        if (_laster) return;
        ++_valg;
        _laster = true; _feil = null; _preview = null;
        try
        {
            var grupper = await Grupper.HentAktiveAsync(_levetid.Token);
            if (!_avsluttet) _grupper = grupper;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception) { if (!_avsluttet) { _feil = "Kunne ikke hente gruppene. Prøv igjen."; _fokus = "eksportfeil"; } }
        finally { if (!_avsluttet) _laster = false; }
    }

    private void Endret()
    {
        ++_valg;
        _preview = null; _feil = null; _status = null; _fokus = null; _kontrollerer = false;
    }

    private async Task KontrollerAsync()
    {
        if (!KanLasteNed || _kontrollerer) return;
        var valg = ++_valg;
        var alt = _alt; var kode = _valgtKode; var fra = _fra; var til = _til;
        _kontrollerer = true; _preview = null; _feil = null; _status = null;
        try
        {
            var preview = await Kommando.HentAsync(alt, kode, fra, til, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _preview = preview; _fokus = "eksportpreview";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception) { if (Gjeldende(valg)) { _feil = "Kunne ikke kontrollere utvalget. Valgene er beholdt. Prøv igjen."; _fokus = "eksportfeil"; } }
        finally { if (Gjeldende(valg)) _kontrollerer = false; }
    }

    private void Avbryt()
    {
        Endret();
        _status = "Kontrollen er avbrutt. Valgene er beholdt.";
        _fokus = "kontroller-eksport";
    }

    private string Nedlastingsurl(string format)
    {
        var deler = new List<string>();
        if (_alt) deler.Add("alt=true");
        else deler.Add($"gruppe={Uri.EscapeDataString(_valgtKode)}");
        if (_fra is { } fra) deler.Add($"fra={fra:yyyy-MM-dd}");
        if (_til is { } til) deler.Add($"til={til:yyyy-MM-dd}");
        return $"/api/eksport/{format}?" + string.Join("&", deler);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_fokus is not { } fokus) return;
        var valg = _valg;
        _fokus = null;
        try
        {
            await using var modul = await Js.InvokeAsync<IJSObjectReference>("import", "./eksportfokus.js");
            if (Gjeldende(valg)) await modul.InvokeVoidAsync("fokuser", _flate, valg, fokus);
        }
        catch (JSDisconnectedException) { }
    }

    private ElementReference _flate;

    public ValueTask DisposeAsync()
    {
        _avsluttet = true; ++_valg;
        _levetid.Cancel(); _levetid.Dispose();
        return ValueTask.CompletedTask;
    }
}
