using System.Globalization;
using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Grupper;
using Aarshjul.Domain;
using Aarshjul.Web.Visning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class GenererAar : IAsyncDisposable
{
    [Inject] private IGenereringstjeneste Generering { get; set; } = default!;
    [Inject] private IGodkjenningsko Ko { get; set; } = default!;
    [Inject] private IGruppetjeneste Grupper { get; set; } = default!;
    [Inject] private Malproevekontekst Kontekst { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private TimeProvider Klokke { get; set; } = default!;
    [Inject] private ILogger<GenererAar> Logg { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "aar")] public string? AarQuery { get; set; }
    [SupplyParameterFromQuery(Name = "lagretAar")] public int? LagretAar { get; set; }
    private const string Pol = "POL";
    private readonly CancellationTokenSource _levetid = new();
    private int _maalaar, _versjon, _previewVersjon, _dialognummer;
    private int? _arbeidsaar;
    private string _aarsfelt = "";
    private string? _lastenoekkel, _feil, _previewFeil, _status;
    private bool _laster, _lastet, _arbeider, _grupperLastet, _previewLaster, _bekrefter, _dialogSynk, _overfoert, _avsluttet;
    private bool _behandler, _navigasjonBlokkert;
    private GenereringsResultat? _resultat;
    private IReadOnlyList<GenerertForslagDto> _forslag = [];
    private IReadOnlyList<GruppeDto> _grupper = [];
    private IReadOnlyList<MalRegelDto> _regler = [];
    private IReadOnlyList<Mallistevisning.Rad> _rader = [];
    private Dictionary<string, DateOnly> _ankre = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, Korttilstand> _tilstand = [];
    private ElementReference _erstattDialog;
    private IJSObjectReference? _modul;
    private DotNetObjectReference<GenererAar>? _egenreferanse;
    // Skrivetilstanden tilhører den pågående operasjonen, også om år/kort lastes på nytt.
    private bool Skriver => _arbeider || _behandler;
    private bool Opptatt => _laster || Skriver;
    private bool KanGenerere => _lastet && !Opptatt && !_previewLaster && Aarsfeil is null && _previewFeil is null;
    private string Dialogvisning => $"{_versjon}-{_dialognummer}";
    private string? Aarsfeil => int.TryParse(_aarsfelt, NumberStyles.None, CultureInfo.InvariantCulture, out var aar) && aar is >= 1 and <= 9999
        ? null : "Budsjettåret må være et heltall fra 1 til 9999.";
    private bool Gjeldende(int versjon, int aar) => !_avsluttet && versjon == _versjon && aar == _maalaar;

    protected override async Task OnParametersSetAsync()
    {
        var noekkel = $"{AarQuery}/{LagretAar}";
        if (_lastenoekkel == noekkel) return;
        _lastenoekkel = noekkel;
        _arbeidsaar = Arbeidsaarkontekst.Les(AarQuery, Klokke);
        _maalaar = _arbeidsaar ?? Arbeidsaarkontekst.Standardaar(Klokke);
        _aarsfelt = _maalaar.ToString(CultureInfo.InvariantCulture);
        _ankre = Kontekst.Genereringsankre(_maalaar);
        _overfoert = Kontekst.TaImotOvergang(_maalaar);
        _resultat = null;
        _forslag = [];
        _regler = [];
        _rader = [];
        _tilstand.Clear();
        _bekrefter = false;
        _dialogSynk = true;
        _status = null;
        await LastAsync();
    }

    private async Task LastAsync()
    {
        var versjon = ++_versjon;
        var aar = _maalaar;
        ++_previewVersjon;
        _previewLaster = false;
        _lastet = false;
        _laster = true;
        _feil = null;
        _previewFeil = null;
        try
        {
            if (!_grupperLastet)
            {
                var grupper = await Grupper.HentAktiveAsync(_levetid.Token);
                if (!Gjeldende(versjon, aar)) return;
                _grupper = grupper;
                _grupperLastet = true;
            }
            var forslag = await Generering.HentGenererteAsync(aar, _levetid.Token);
            if (!Gjeldende(versjon, aar)) return;
            var snapshot = await Generering.ProevAsync(new(aar, new Dictionary<string, DateOnly>(_ankre, StringComparer.OrdinalIgnoreCase)), _levetid.Token);
            if (!Gjeldende(versjon, aar)) return;
            _forslag = forslag;
            _regler = snapshot.Regler;
            _rader = Mallistevisning.Lag(snapshot);
            _tilstand.Clear();
            _lastet = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (!Gjeldende(versjon, aar)) return;
            Logg.LogError(e, "Kunne ikke laste genereringsflaten for budsjettår {Aar}.", aar);
            _feil = "Kunne ikke laste årsmalen og forslagene akkurat nå. Prøv igjen.";
        }
        finally { if (Gjeldende(versjon, aar)) _laster = false; }
    }

    private void ByttAar(int? aar) { if (!Opptatt) Nav.NavigateTo(Arbeidsaarkontekst.Lenke("/admin/generer", aar)); }
    private Task FoerNavigasjon(LocationChangingContext kontekst)
    {
        if (!Skriver) return Task.CompletedTask;
        kontekst.PreventNavigation();
        _navigasjonBlokkert = true;
        return InvokeAsync(StateHasChanged);
    }
    private void ByttMaalaar(ChangeEventArgs e)
    {
        if (Opptatt) return;
        _aarsfelt = e.Value?.ToString() ?? "";
        if (Aarsfeil is null && int.TryParse(_aarsfelt, CultureInfo.InvariantCulture, out var aar) && aar != _maalaar) ByttAar(aar);
    }

    private async Task DatoEndretAsync((string Loep, DateOnly? Dato) endring)
    {
        if (Opptatt || _bekrefter || Aarsfeil is not null) return;
        if (endring.Dato is { } dato) _ankre[endring.Loep] = dato;
        else _ankre.Remove(endring.Loep);
        await OppdaterProeveAsync();
    }
    private async Task FjernAlleAsync() { if (Opptatt || _bekrefter) return; _ankre.Clear(); await OppdaterProeveAsync(); }
    private async Task OppdaterProeveAsync()
    {
        var versjon = _versjon;
        var aar = _maalaar;
        var previewVersjon = ++_previewVersjon;
        var ankre = new Dictionary<string, DateOnly>(_ankre, StringComparer.OrdinalIgnoreCase);
        _previewLaster = true;
        _previewFeil = null;
        try
        {
            var snapshot = await Generering.ProevAsync(new(aar, ankre), _levetid.Token);
            if (!Gjeldende(versjon, aar) || previewVersjon != _previewVersjon) return;
            _regler = snapshot.Regler;
            _rader = Mallistevisning.Lag(snapshot);
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (!Gjeldende(versjon, aar) || previewVersjon != _previewVersjon) return;
            Logg.LogError(e, "Kunne ikke kontrollere årsmalen før generering.");
            _previewFeil = "Kunne ikke oppdatere kontrollen akkurat nå. Midlertidige datoer og lagrede forslag er beholdt. Prøv igjen.";
        }
        finally { if (Gjeldende(versjon, aar) && previewVersjon == _previewVersjon) _previewLaster = false; }
    }

    private async Task BeOmGenerering()
    {
        if (!KanGenerere || _bekrefter) return;
        if (_forslag.Count == 0) { await BeregnAsync(); return; }
        _bekrefter = true;
        ++_dialognummer;
        _dialogSynk = true;
    }

    private async Task AvbrytErstatningAsync()
    {
        if (_arbeider) return;
        _bekrefter = false;
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _erstattDialog);
    }

    [JSInvokable]
    public Task AvbruttFraTastaturAsync(string visning)
    {
        if (visning == Dialogvisning && !_arbeider) { _bekrefter = false; return InvokeAsync(StateHasChanged); }
        return Task.CompletedTask;
    }

    private async Task BekreftErstatningAsync()
    {
        if (!_bekrefter || Opptatt || Aarsfeil is not null) return;
        _bekrefter = false;
        _dialogSynk = true;
        // BeregnAsync reserverer handlingen og fanger år/inndata før første await.
        // Dialogen lukkes av den samme renderen som deaktiverer kort og innsendingsknapper.
        await BeregnAsync();
    }

    private async Task BeregnAsync()
    {
        if (Opptatt || !_lastet || Aarsfeil is not null) return;
        var versjon = _versjon;
        var aar = _maalaar;
        var ankre = new Dictionary<string, DateOnly>(_ankre, StringComparer.OrdinalIgnoreCase);
        _arbeider = true;
        _navigasjonBlokkert = false;
        _feil = null;
        _status = null;
        _resultat = null;
        var lagret = false;
        try
        {
            var resultat = await Generering.GenererAsync(new(aar, ankre), _levetid.Token);
            lagret = true;
            if (!Gjeldende(versjon, aar)) return;
            _resultat = resultat;
            var forslag = await Generering.HentGenererteAsync(aar, _levetid.Token);
            if (!Gjeldende(versjon, aar)) return;
            _forslag = forslag;
            _tilstand.Clear();
            await OppdaterProeveAsync();
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (Gjeldende(versjon, aar)) _feil = e.Message; }
        catch (Exception e)
        {
            if (!Gjeldende(versjon, aar)) return;
            Logg.LogError(e, "Kunne ikke fullføre genereringsflaten for budsjettår {Aar}.", aar);
            _feil = lagret ? "Forslagene er beregnet og lagret, men listen kunne ikke oppdateres. Last siden på nytt før videre behandling."
                : "Kunne ikke lagre genereringen akkurat nå. De tidligere forslagene og datoene dine er beholdt. Prøv igjen.";
            if (lagret) { _forslag = []; _tilstand.Clear(); _lastet = false; }
        }
        finally { _arbeider = false; }
    }

    private Korttilstand Tilstand(GenerertForslagDto f) => _tilstand.TryGetValue(f.ForslagId, out var t) ? t : _tilstand[f.ForslagId] = new();
    private bool GjeldendeKort(GenerertForslagDto f, Korttilstand t) => _forslag.Any(x => x.ForslagId == f.ForslagId)
        && _tilstand.TryGetValue(f.ForslagId, out var gjeldende) && ReferenceEquals(t, gjeldende);
    private void StartGodkjenn(GenerertForslagDto f, Korttilstand t)
    {
        if (Opptatt || _bekrefter || !GjeldendeKort(f, t)) return;
        LukkAlleKort();
        t.Koder = f.ForeslaattSynlighet.Where(k => !string.Equals(k, Pol, StringComparison.OrdinalIgnoreCase) && _grupper.Any(g => g.Kode == k)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        t.Modus = Kortmodus.Godkjenn;
    }
    private void StartAvvis(GenerertForslagDto f, Korttilstand t) { if (Opptatt || _bekrefter || !GjeldendeKort(f, t)) return; LukkAlleKort(); t.Modus = Kortmodus.Avvis; }
    private void LukkAlleKort() { foreach (var t in _tilstand.Values) LukkKort(t); }
    private void AvbrytKort(Korttilstand t) { if (!Opptatt) LukkKort(t); }
    private static void LukkKort(Korttilstand t) { t.Modus = Kortmodus.Ingen; t.Koder.Clear(); t.TentativBekreftet = false; t.Begrunnelse = null; t.Feil = null; }
    private void VekslKode(Korttilstand t, string kode, bool paa) { if (Opptatt) return; if (paa) t.Koder.Add(kode); else t.Koder.Remove(kode); }
    private void Juster(GenerertForslagDto f)
    {
        if (!Opptatt && !_bekrefter && _forslag.Any(x => x.ForslagId == f.ForslagId))
            Nav.NavigateTo($"{Arbeidsaarkontekst.Lenke($"/admin/frist/juster/{f.ForslagId}", _maalaar)}&fra=generering");
    }
    private Task Godkjenn(GenerertForslagDto f, Korttilstand t) => BehandleAsync(f, t, true);
    private Task Avvis(GenerertForslagDto f, Korttilstand t) => BehandleAsync(f, t, false);
    private async Task BehandleAsync(GenerertForslagDto f, Korttilstand t, bool godkjenn)
    {
        if (Opptatt || _bekrefter || !GjeldendeKort(f, t) || (godkjenn && f.Tentativ && !t.TentativBekreftet)) return;
        var versjon = _versjon;
        var aar = _maalaar;
        t.Arbeider = true;
        _behandler = true;
        _navigasjonBlokkert = false;
        t.Feil = null;
        var behandlet = false;
        try
        {
            if (godkjenn) await Ko.GodkjennAsync(new() { ForslagId = f.ForslagId, Synlighetskoder = t.Koder.ToList(), PolBekreftet = t.Koder.Contains(Pol) }, _levetid.Token);
            else await Ko.AvvisAsync(f.ForslagId, string.IsNullOrWhiteSpace(t.Begrunnelse) ? null : t.Begrunnelse, _levetid.Token);
            behandlet = true;
            if (!Gjeldende(versjon, aar)) return;
            var forslag = await Generering.HentGenererteAsync(aar, _levetid.Token);
            if (!Gjeldende(versjon, aar)) return;
            _forslag = forslag;
            _tilstand.Clear();
            _status = godkjenn ? $"«{f.Tittel}» er godkjent og publisert i budsjettår {aar}." : $"«{f.Tittel}» er avvist.";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (Gjeldende(versjon, aar) && GjeldendeKort(f, t)) t.Feil = e.Message; }
        catch (Exception e)
        {
            if (!Gjeldende(versjon, aar)) return;
            Logg.LogError(e, "Kunne ikke fullføre behandling av generert forslag.");
            if (behandlet) { _forslag = []; _tilstand.Clear(); _lastet = false; _feil = "Forslaget er behandlet, men listen kunne ikke oppdateres. Last forslagene på nytt før du fortsetter."; }
            else if (GjeldendeKort(f, t)) t.Feil = "Kunne ikke behandle forslaget akkurat nå. Valgene dine er beholdt. Prøv igjen.";
        }
        finally { t.Arbeider = false; _behandler = false; }
    }

    private static string FormatDato(DateOnly? dato, Datopresisjon presisjon, Datokvalifikator? kvalifikator) => dato is { } d
        ? presisjon == Datopresisjon.Dag ? d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : Visningformat.FormatDato(d, presisjon, kvalifikator) : "Ingen dato";
    private static string? Maskindato(DateOnly? dato, Datopresisjon presisjon) => dato?.ToString(presisjon == Datopresisjon.Maaned ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_dialogSynk) return;
        _dialogSynk = false;
        var visning = Dialogvisning;
        try
        {
            _modul ??= await Js.InvokeAsync<IJSObjectReference>("import", "./genereringsdialog.js");
            if (_avsluttet) return;
            _egenreferanse ??= DotNetObjectReference.Create(this);
            await _modul.InvokeVoidAsync("synkroniser", _erstattDialog, visning, _egenreferanse);
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _avsluttet = true;
        _levetid.Cancel(); _levetid.Dispose();
        _egenreferanse?.Dispose();
        if (_modul is not null) { try { await _modul.DisposeAsync(); } catch (JSDisconnectedException) { } }
    }
    private enum Kortmodus { Ingen, Godkjenn, Avvis }
    private sealed class Korttilstand
    {
        public Kortmodus Modus { get; set; }
        public HashSet<string> Koder { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool TentativBekreftet { get; set; }
        public string? Begrunnelse { get; set; }
        public string? Feil { get; set; }
        public bool Arbeider { get; set; }
    }
}
