using System.Globalization;
using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Mal : IAsyncDisposable
{
    [Inject] private IMaltjeneste Maltjeneste { get; set; } = default!;
    [Inject] private Mallistekontekst Kontekst { get; set; } = default!;
    [Inject] private TimeProvider Klokke { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<Mal> Logg { get; set; } = default!;
    private static readonly Kategori[] Kategoriene = [Kategori.Budsjett, Kategori.Gulbok, Kategori.Regnskap];
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<MalRegelDto> _regler = [];
    private IReadOnlyList<Mallistevisning.Rad> _rader = [];
    private IReadOnlyList<MalRegelForhaandsvisning> _preview = [];
    private Mallistevisning.Rad? _valgt;
    private MalRegelDto? _slett;
    private string? _feil, _status, _slettefeil, _previewFeil, _returfokus;
    private Guid? _lagretRetur, _lagretForStatus;
    private int _vistAar, _lastVersjon, _previewVersjon, _foerstePreviewaar;
    private bool _lastet, _laster, _sletter, _aapneSlett, _aapneVisning, _datoer, _previewLaster, _fokuserRetur, _avsluttet;
    private ElementReference _slettDialog, _visDialog;
    private IJSObjectReference? _modul;
    private IReadOnlyList<Mallistevisning.Rad> VisteRader => Mallistevisning.Velg(_rader, Kontekst);
    private bool LagretSkjult => _lagretForStatus is { } id && !VisteRader.Any(r => r.Regel.Id == id);
    private string? Aarsfeil => ProevAar(out _) ? null : "Budsjettåret må være et heltall fra 1 til 9999.";
    private string Sorteringstekst => Kontekst.Sortering switch { Regelsortering.Senest => "Senest først", Regelsortering.Tittel => "Tittel – A til Å", _ => "Tidligst først" };
    private bool ProevAar(out int aar) => int.TryParse(Kontekst.Aarsfelt, NumberStyles.None, CultureInfo.InvariantCulture, out aar) && aar is >= 1 and <= 9999;

    protected override async Task OnInitializedAsync()
    {
        Kontekst.Initialiser(Datohjelp.Idag(Klokke).Year);
        _returfokus = Kontekst.Returfokus;
        _lagretRetur = Kontekst.LagretRegel;
        Kontekst.Returfokus = null;
        Kontekst.LagretRegel = null;
        await LastAsync();
    }

    private async Task LastAsync()
    {
        var versjon = ++_lastVersjon;
        var aar = Kontekst.Budsjettaar!.Value;
        _laster = true;
        _feil = null;
        try
        {
            var snapshot = await Maltjeneste.ForhaandsvisAlleAsync(aar, _levetid.Token);
            if (_avsluttet || versjon != _lastVersjon) return;
            _regler = snapshot.Regler;
            _rader = Mallistevisning.Lag(snapshot);
            _vistAar = snapshot.Budsjettaar;
            _lastet = true;
            if (_lagretRetur is { } lagretId)
            {
                var regel = _regler.FirstOrDefault(r => r.Id == lagretId);
                var skjult = !VisteRader.Any(r => r.Regel.Id == lagretId);
                _lagretForStatus = regel is null ? null : lagretId;
                _status = regel is null ? "Regelen er lagret, men finnes ikke lenger i malen."
                    : $"«{Mallistevisning.Navn(regel)}» er lagret. Beregningen er oppdatert; valgene dine er beholdt.";
                _returfokus = skjult ? "malliste-tittel" : $"rediger-{lagretId}";
                _lagretRetur = null;
            }
            if (_returfokus is not null) _fokuserRetur = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (_avsluttet || versjon != _lastVersjon) return;
            Logg.LogError(e, "Kunne ikke laste og beregne gjentaksregler.");
            _feil = "Kunne ikke laste og beregne gjentaksreglene akkurat nå. Prøv igjen.";
        }
        finally { if (!_avsluttet && versjon == _lastVersjon) _laster = false; }
    }

    private async Task AarEndretAsync(ChangeEventArgs e)
    {
        Kontekst.Aarsfelt = e.Value?.ToString() ?? "";
        if (!ProevAar(out var aar) || aar == Kontekst.Budsjettaar) return;
        Kontekst.Budsjettaar = aar;
        await LastAsync();
    }
    private void KategoriEndret(Kategori kategori, ChangeEventArgs e)
    {
        if (e.Value is true) Kontekst.Kategorier.Add(kategori);
        else Kontekst.Kategorier.Remove(kategori);
    }
    private void VelgAlle() => Kontekst.Kategorier.UnionWith(Kategoriene);
    private void NullstillEtterLagring() => Kontekst.NullstillFiltre();
    private void AapneRedigering(Guid id)
    {
        Kontekst.AapnetRegel = id;
        Kontekst.Returfokus = $"rediger-{id}";
    }

    private async Task SeDatoerAsync(Mallistevisning.Rad rad)
    {
        var versjon = ++_previewVersjon;
        _valgt = rad;
        _datoer = true;
        _preview = [];
        _previewFeil = null;
        _previewLaster = true;
        _foerstePreviewaar = Math.Min(_vistAar, 9995);
        _aapneVisning = true;
        try
        {
            var r = rad.Regel;
            var data = new MalRegelInndata { Loep = r.Loep, Tittel = r.Tittel, Kategori = r.Kategori,
                Regeltype = r.Regeltype, Regelparametre = r.Regelparametre, Valgaarssensitiv = r.Valgaarssensitiv };
            // Eldre importer kan mangle tittel; kun dette ulagrete preview bruker visningsnavnet.
            if (string.IsNullOrWhiteSpace(data.Tittel)) data.Tittel = rad.Tittel;
            var resultat = await Maltjeneste.ForhaandsvisAsync(data, _foerstePreviewaar, r.Id, _levetid.Token);
            if (!_avsluttet && versjon == _previewVersjon) _preview = resultat;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { if (!_avsluttet && versjon == _previewVersjon) _previewFeil = e.Message; }
        catch (Exception e)
        {
            if (!_avsluttet && versjon == _previewVersjon)
            {
                Logg.LogError(e, "Kunne ikke forhåndsvise gjentaksregel fra listen.");
                _previewFeil = "Kunne ikke beregne femårsvisningen akkurat nå. Lukk og prøv igjen.";
            }
        }
        finally { if (!_avsluttet && versjon == _previewVersjon) _previewLaster = false; }
    }
    private void SeSammenheng(Mallistevisning.Rad rad)
    {
        ++_previewVersjon;
        _valgt = rad;
        _datoer = false;
        _aapneVisning = true;
    }
    private async Task LukkVisningAsync()
    {
        ++_previewVersjon;
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _visDialog);
    }
    private void BeOmSlett(MalRegelDto regel) { if (_sletter) return; _slett = regel; _slettefeil = null; _aapneSlett = true; }
    private async Task AvbrytSlettAsync()
    {
        if (_sletter) return;
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _slettDialog);
        _slett = null;
    }
    private async Task SlettAsync()
    {
        if (_sletter || _slett is null) return;
        _sletter = true;
        _slettefeil = null;
        try
        {
            await Maltjeneste.SlettAsync(_slett.Id, _levetid.Token);
            if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _slettDialog);
            _status = $"«{Mallistevisning.Navn(_slett)}» er slettet fra årsmalen. Frister og forslag er uendret.";
            _lagretForStatus = null;
            _slett = null;
            _returfokus = "malliste-tittel";
            await LastAsync();
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception e) { Logg.LogError(e, "Kunne ikke slette gjentaksregel."); _slettefeil = "Kunne ikke slette regelen akkurat nå. Prøv igjen eller avbryt."; }
        finally { _sletter = false; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_fokuserRetur || _aapneSlett || _aapneVisning)
        {
            try
            {
                _modul ??= await Js.InvokeAsync<IJSObjectReference>("import", "./regelbygger.js");
                if (_avsluttet) return;
                if (_fokuserRetur)
                {
                    _fokuserRetur = false;
                    var synlig = _returfokus is not null && VisteRader.Any(r => $"rediger-{r.Regel.Id}" == _returfokus);
                    await _modul.InvokeVoidAsync("fokuser", synlig ? _returfokus : "malliste-tittel");
                    _returfokus = null;
                }
                if (_aapneSlett) { _aapneSlett = false; await _modul.InvokeVoidAsync("vis", _slettDialog); }
                if (_aapneVisning) { _aapneVisning = false; await _modul.InvokeVoidAsync("vis", _visDialog); }
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
