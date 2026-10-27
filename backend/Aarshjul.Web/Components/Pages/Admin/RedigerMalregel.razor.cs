using System.Globalization;
using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using static Aarshjul.Web.Components.Pages.Admin.Malregelutkast;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class RedigerMalregel : IAsyncDisposable
{
    [Inject] private IMaltjeneste Mal { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private TimeProvider Klokke { get; set; } = default!;
    [Inject] private ILogger<RedigerMalregel> Logg { get; set; } = default!;
    [Inject] private Mallistekontekst Listekontekst { get; set; } = default!;
    [Parameter] public Guid? Id { get; set; }
    [SupplyParameterFromQuery(Name = "kopierFra")] public Guid? KopierFra { get; set; }

    private static readonly (Valg Type, string Navn, string Hjelp)[] Valgene =
    [
        (Valg.FastDato, "Fast dato", "Samme dag og måned hvert år"),
        (Valg.Forekomst, "Ukedag i måneden", "For eksempel andre mandag i mars"),
        (Valg.FraDato, "Ukedag fra og med en dato", "For eksempel mandag fra 20. juli"),
        (Valg.Milepael, "Før eller etter en milepæl", "Kalenderdager fra et ankerløp")
    ];
    private static readonly CultureInfo Norsk = CultureInfo.GetCultureInfo("nb-NO");
    private readonly CancellationTokenSource _levetid = new();
    private Malregelutkast? _utkast;
    private Guid? _utkastId;
    private IReadOnlyList<MalRegelDto> _alleRegler = [];
    private IReadOnlyList<MalRegelForhaandsvisning> _preview = [];
    private Dictionary<string, string> _feltfeil = [];
    private string? _lastfeil, _feilmelding, _previewFeil, _aarsfeil, _ventendeUri, _lastenoekkel;
    private string _opprinnelig = "", _opprinneligLoep = "", _foersteAar = "";
    private bool _laster, _lagrer, _kopi, _loepRoert, _tillatNavigasjon, _visValidering, _fokuserFeil, _aapneDialog, _previewLaster, _avsluttet;
    private int _lastVersjon, _utkastVersjon, _previewVersjon;
    private ElementReference _feilfelt, _forlatDialog;
    private IJSObjectReference? _modul;
    private string Sidetittel => Id is not null ? "Rediger gjentaksregel" : KopierFra is not null ? "Kopier gjentaksregel" : "Nye gjentaksregler";
    private bool ErEndret => !_tillatNavigasjon && _utkast is not null && (_kopi || _utkast.Bilde() != _opprinnelig);
    private int AntallAvhengige => Malregelutkast.Avhengige(_alleRegler, _opprinneligLoep);
    private IEnumerable<MalRegelDto> Ankerkandidater => _alleRegler.Where(r => r.Id != Id).GroupBy(r => r.Loep, StringComparer.OrdinalIgnoreCase).Select(g => g.First());

    protected override async Task OnParametersSetAsync()
    {
        var noekkel = $"{Id}/{KopierFra}";
        if (_lastenoekkel == noekkel) return;
        _lastenoekkel = noekkel;
        await LastAsync();
    }

    private async Task LastAsync()
    {
        if (_avsluttet) return;
        var versjon = ++_lastVersjon;
        var maalId = Id;
        var kopierFra = KopierFra;
        var kildeId = maalId ?? kopierFra;
        if (maalId is { } aapnetId)
        {
            Listekontekst.AapnetRegel = aapnetId;
            Listekontekst.Returfokus = $"rediger-{aapnetId}";
        }
        ++_previewVersjon;
        _laster = true;
        _lagrer = false;
        _utkast = null;
        _utkastId = null;
        _lastfeil = null;
        _feilmelding = null;
        _previewFeil = null;
        _aarsfeil = null;
        _previewLaster = false;
        _fokuserFeil = false;
        _feltfeil.Clear();
        _preview = [];
        _visValidering = false;
        _tillatNavigasjon = false;
        try
        {
            var alleRegler = await Mal.HentAlleAsync(_levetid.Token);
            if (!ErGjeldende(versjon)) return;
            var data = kildeId is { } id ? await Mal.HentForRedigeringAsync(id, _levetid.Token) : new MalRegelInndata();
            if (!ErGjeldende(versjon) || data is null) return;
            _alleRegler = alleRegler;
            _utkast = FraInndata(data);
            _utkastId = maalId;
            _utkastVersjon = versjon;
            _kopi = maalId is null && kopierFra is not null;
            if (_kopi) _utkast.Tittel += " (kopi)";
            _loepRoert = kildeId is not null;
            _opprinneligLoep = _utkast.Loep;
            _opprinnelig = _utkast.Bilde();
            _foersteAar = Datohjelp.Idag(Klokke).Year.ToString(CultureInfo.InvariantCulture);
            await OppdaterPreviewAsync();
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (!ErGjeldende(versjon)) return;
            Logg.LogError(e, "Kunne ikke laste gjentaksregel.");
            _lastfeil = "Kunne ikke laste gjentaksregelen akkurat nå. Prøv igjen.";
        }
        finally { if (ErGjeldende(versjon)) _laster = false; }
    }

    private bool ErGjeldende(int versjon) => !_avsluttet && versjon == _lastVersjon;

    private async Task TittelEndretAsync(ChangeEventArgs e)
    {
        _utkast!.Tittel = e.Value?.ToString() ?? "";
        if (Id is null && !_loepRoert)
            _utkast.Loep = string.Join('-', _utkast.Tittel.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        await EndretAsync();
    }
    private async Task LoepEndretAsync(ChangeEventArgs e) { _loepRoert = true; _utkast!.Loep = e.Value?.ToString() ?? ""; await EndretAsync(); }
    private async Task ByttTypeAsync(Valg type) { _utkast!.Type = type; await EndretAsync(); }
    private async Task KorrigerAsync() { _utkast!.Korriger(); await EndretAsync(); }
    private async Task EndretAsync()
    {
        if (_visValidering)
        {
            _utkast!.ProevInndata(out _, out _feltfeil);
            _feilmelding = _feltfeil.Count > 0 ? "Regelen er ikke lagret. Rett feltene nedenfor." : null;
        }
        await OppdaterPreviewAsync();
    }

    private async Task OppdaterPreviewAsync()
    {
        if (!ErGjeldende(_utkastVersjon) || _utkast is null) return;
        var lastVersjon = _utkastVersjon;
        var versjon = ++_previewVersjon;
        var maalId = _utkastId;
        bool GjeldendePreview() => ErGjeldende(lastVersjon) && versjon == _previewVersjon;
        _preview = [];
        _previewFeil = null;
        _aarsfeil = null;
        _previewLaster = false;
        if (!int.TryParse(_foersteAar, NumberStyles.None, CultureInfo.InvariantCulture, out var aar) || aar is < 1 or > 9995)
        {
            _aarsfeil = "Første budsjettår må være et heltall fra 1 til 9995.";
            return;
        }
        if (_utkast is null || !_utkast.ProevInndata(out var data, out _))
        {
            _previewFeil = "Fyll ut gyldige felt for å se fem års beregning.";
            return;
        }
        _previewLaster = true;
        try
        {
            var resultat = await Mal.ForhaandsvisAsync(data, aar, maalId, _levetid.Token);
            if (GjeldendePreview()) _preview = resultat;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (GjeldendePreview()) _previewFeil = e.Message; }
        catch (Exception e)
        {
            if (!GjeldendePreview()) return;
            Logg.LogError(e, "Kunne ikke forhåndsvise gjentaksregel.");
            _previewFeil = "Forhåndsvisningen kunne ikke beregnes akkurat nå. Feltene er beholdt; du kan fortsatt lagre en gyldig regel.";
        }
        finally { if (GjeldendePreview()) _previewLaster = false; }
    }

    private async Task LagreAsync()
    {
        if (_laster || _lagrer || _tillatNavigasjon || _utkast is null || !ErGjeldende(_utkastVersjon)) return;
        var versjon = _utkastVersjon;
        var maalId = _utkastId;
        _visValidering = true;
        _feilmelding = null;
        if (!_utkast.ProevInndata(out var data, out _feltfeil))
        {
            _feilmelding = "Regelen er ikke lagret. Rett feltene nedenfor.";
            _fokuserFeil = true;
            return;
        }
        _lagrer = true;
        try
        {
            var lagretId = maalId;
            if (maalId is { } id) await Mal.OppdaterAsync(id, data, _levetid.Token);
            else lagretId = await Mal.OpprettAsync(data, _levetid.Token);
            if (!ErGjeldende(versjon)) return;
            Listekontekst.LagretRegel = lagretId;
            _tillatNavigasjon = true;
            Nav.NavigateTo("/admin/mal");
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (ErGjeldende(versjon)) { _feilmelding = e.Message; _fokuserFeil = true; } }
        catch (Exception e)
        {
            if (!ErGjeldende(versjon)) return;
            Logg.LogError(e, "Kunne ikke lagre gjentaksregel.");
            _feilmelding = "Kunne ikke lagre regelen akkurat nå. Alle feltene er beholdt. Prøv igjen.";
            _fokuserFeil = true;
        }
        finally { if (ErGjeldende(versjon)) _lagrer = false; }
    }

    private Task FoerNavigasjon(LocationChangingContext kontekst)
    {
        if (!_tillatNavigasjon && (_lagrer || ErEndret))
        {
            kontekst.PreventNavigation();
            if (!_lagrer) { _ventendeUri = kontekst.TargetLocation; _aapneDialog = true; _ = InvokeAsync(StateHasChanged); }
        }
        return Task.CompletedTask;
    }
    private async Task FortsettAsync() { _ventendeUri = null; if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _forlatDialog); }
    private async Task ForkastAsync()
    {
        var maal = _ventendeUri;
        await FortsettAsync();
        if (maal is null) return;
        _tillatNavigasjon = true;
        Nav.NavigateTo(maal);
    }
    private async Task FokuserFeltAsync(string id) { _modul ??= await Js.InvokeAsync<IJSObjectReference>("import", "./regelbygger.js"); await _modul.InvokeVoidAsync("fokuser", id); }
    private string Ugyldig(string id) => _feltfeil.ContainsKey(id) ? "true" : "false";
    private string? Feilreferanse(string id) => _feltfeil.ContainsKey(id) ? $"feil-{id}" : null;
    private RenderFragment Feltfeil(string id) => b =>
    {
        if (!_feltfeil.TryGetValue(id, out var tekst)) return;
        b.OpenElement(0, "small"); b.AddAttribute(1, "class", "regelfeltfeil"); b.AddAttribute(2, "id", $"feil-{id}"); b.AddContent(3, tekst); b.CloseElement();
    };
    private static string Dato(MalRegelForhaandsvisning rad) => rad.Dato is not { } dato ? "Ingen dato" : rad.Presisjon == Datopresisjon.Maaned ? $"{dato.ToString("MMMM yyyy", Norsk)} (tentativ)" : $"{dato.ToString("dddd d. MMMM yyyy", Norsk)}{(rad.Tentativ ? " (tentativ)" : "")}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_fokuserFeil) { _fokuserFeil = false; await _feilfelt.FocusAsync(); }
        if (_aapneDialog)
        {
            _aapneDialog = false;
            try
            {
                _modul ??= await Js.InvokeAsync<IJSObjectReference>("import", "./regelbygger.js");
                if (!_avsluttet) await _modul.InvokeVoidAsync("vis", _forlatDialog);
            }
            catch (JSDisconnectedException) { }
        }
    }
    public async ValueTask DisposeAsync()
    {
        _avsluttet = true;
        _levetid.Cancel(); _levetid.Dispose();
        if (_modul is not null) { try { await _modul.DisposeAsync(); } catch (JSDisconnectedException) { } }
    }
}
