using Aarshjul.Application;
using Aarshjul.Application.Grupper;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Grupper : IAsyncDisposable
{
    [Inject] private IGruppetjeneste Gruppetjeneste { get; set; } = default!;
    [Inject] private GruppeadministrasjonKommando Kommando { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<Grupper> Logg { get; set; } = default!;
    private readonly CancellationTokenSource _levetid = new();
    private readonly Dictionary<string, string> _feltfeil = [];
    private IReadOnlyList<GruppeadministrasjonDto> _grupper = [];
    private string _kode = "", _navn = "";
    private string? _feil, _lastfeil, _melding;
    private bool _aktiv, _redigerer, _oppretter, _arbeider, _laster = true, _aapneDialog, _fokuserFeil, _avsluttet;
    private int _visning;
    private ElementReference _dialog, _feilfelt;
    private IJSObjectReference? _modul;
    private DotNetObjectReference<Grupper>? _referanse;

    protected override Task OnInitializedAsync() => LastAsync();

    private async Task LastAsync()
    {
        _laster = true; _lastfeil = null;
        try
        {
            var grupper = await Gruppetjeneste.HentAlleAsync(_levetid.Token);
            if (!_avsluttet) _grupper = grupper;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (_avsluttet) return;
            Logg.LogError(e, "Kunne ikke laste synlighetsgrupper.");
            _lastfeil = "Kunne ikke laste gruppene akkurat nå. Prøv igjen.";
        }
        finally { if (!_avsluttet) _laster = false; }
    }

    private void NyGruppe()
    {
        if (_arbeider || _laster || _redigerer) return;
        Aapne(true, "", "", true);
    }

    private void Rediger(GruppeadministrasjonDto gruppe)
    {
        if (_arbeider || _redigerer) return;
        Aapne(false, gruppe.Kode, gruppe.Navn, gruppe.Aktiv);
    }

    private void Aapne(bool oppretter, string kode, string navn, bool aktiv)
    {
        _oppretter = oppretter; _kode = kode; _navn = navn; _aktiv = aktiv;
        _feil = null; _melding = null; _feltfeil.Clear(); _fokuserFeil = false;
        _redigerer = true; _aapneDialog = true; _visning++;
    }

    private string Ugyldig(string felt) => _feltfeil.ContainsKey(felt) ? "true" : "false";
    private string Beskrivelse(string felt, string hjelp) => _feltfeil.ContainsKey(felt) ? $"{hjelp} gruppe-{felt}feil" : hjelp;

    private async Task LagreAsync()
    {
        if (_arbeider || !_redigerer || _avsluttet) return;
        _feil = null; _feltfeil.Clear();
        var kode = _kode.Trim();
        var navn = _navn.Trim();
        if (_oppretter && (kode.Length is < 1 or > 64 || !char.IsAsciiLetter(kode[0])
            || kode.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')))
            _feltfeil["kode"] = "Kode må ha 1–64 tegn, begynne med en bokstav og bare inneholde A–Z, 0–9 og bindestrek.";
        if (navn.Length is < 1 or > 256) _feltfeil["navn"] = "Visningsnavn må ha 1–256 tegn.";
        if (_feltfeil.Count > 0) { _feil = "Kontroller feltene før du lagrer."; _fokuserFeil = true; return; }
        var visning = _visning;
        var oppretter = _oppretter;
        var aktiv = _aktiv;
        _arbeider = true;
        try
        {
            if (oppretter) await Kommando.OpprettAsync(kode, navn, _levetid.Token);
            else await Kommando.OppdaterAsync(kode, navn, aktiv, _levetid.Token);
            if (_avsluttet || visning != _visning) return;
            await LukkAsync(visning);
            _melding = oppretter ? "Gruppen er opprettet." : "Endringene er lagret.";
            await LastAsync();
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { VisFeil(visning, e.Message); }
        catch (UnauthorizedAccessException) { VisFeil(visning, "Du har ikke tilgang til å vedlikeholde grupper."); }
        catch (Exception e)
        {
            if (_avsluttet) return;
            Logg.LogError(e, "Kunne ikke lagre synlighetsgruppe.");
            VisFeil(visning, "Kunne ikke lagre gruppen akkurat nå. Verdiene er beholdt. Prøv igjen.");
        }
        finally { if (!_avsluttet && visning == _visning) _arbeider = false; }
    }

    private void VisFeil(int visning, string feil)
    {
        if (_avsluttet || visning != _visning) return;
        _feil = feil; _fokuserFeil = true;
    }

    private Task AvbrytAsync() => _arbeider ? Task.CompletedTask : LukkAsync(_visning);

    private async Task LukkAsync(int visning)
    {
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _dialog, visning);
        if (_avsluttet || visning != _visning) return;
        _redigerer = false; _aapneDialog = false; _feil = null; _feltfeil.Clear();
    }

    [JSInvokable]
    public async Task AvbruttFraTastaturAsync(int visning)
    {
        if (_arbeider || _avsluttet || visning != _visning) return;
        await LukkAsync(visning);
        await InvokeAsync(StateHasChanged);
    }

    private void FoerNavigasjon(LocationChangingContext context)
    {
        if (_arbeider) context.PreventNavigation();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_avsluttet) return;
        if (_aapneDialog)
        {
            _aapneDialog = false;
            var visning = _visning;
            if (_modul is null)
            {
                var modul = await Js.InvokeAsync<IJSObjectReference>("import", "./gruppedialog.js");
                if (_avsluttet || _modul is not null) await modul.DisposeAsync();
                else _modul = modul;
            }
            if (_avsluttet || !_redigerer || visning != _visning) return;
            _referanse ??= DotNetObjectReference.Create(this);
            await _modul!.InvokeVoidAsync("vis", _dialog, visning, _referanse);
        }
        if (_fokuserFeil && _redigerer && !_avsluttet)
        {
            _fokuserFeil = false;
            await _feilfelt.FocusAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _avsluttet = true; _levetid.Cancel(); _levetid.Dispose(); _referanse?.Dispose();
        if (_modul is null) return;
        try { await _modul.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
