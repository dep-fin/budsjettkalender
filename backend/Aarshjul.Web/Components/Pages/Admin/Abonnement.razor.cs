using Aarshjul.Application.Grupper;
using Aarshjul.Application.Kalender;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Abonnement : IAsyncDisposable
{
    [Inject] private IKalenderabonnement Feedtjeneste { get; set; } = default!;
    [Inject] private IGruppetjeneste Grupper { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    private enum Handling { Opprett, SkruAv, SkruPaa, Slett }
    // '*' er utenfor gruppekodenes grammatikk og kan aldri kollidere med en aktiv gruppe.
    private const string AltPublikum = "*";
    private sealed record Bekreftelse(Handling Type, Guid Id, string? Gruppekode, string Publikum);
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<GruppeDto> _grupper = [];
    private IReadOnlyList<KalenderabonnementDto> _abonnementer = [];
    private string _valgtKode = "";
    private string? _feil, _status, _fokus;
    private bool _laster, _lastet, _arbeider, _avsluttet, _aapneDialog;
    private int _valg;
    private Bekreftelse? _venter;
    private ElementReference _dialog;
    private IJSObjectReference? _modul;
    private Task<IJSObjectReference>? _modullasting;
    private DotNetObjectReference<Abonnement>? _referanse;
    private bool Laast => _laster || !_lastet || _arbeider;
    private bool Gjeldende(int valg) => !_avsluttet && _valg == valg;
    private string ValgtPublikum => _valgtKode == AltPublikum ? "Alt (fullt innsyn – FIN-internt)"
        : _grupper.FirstOrDefault(g => g.Kode == _valgtKode) is { } g ? $"{g.Navn} ({g.Kode})" : "Ingen publikum er valgt";
    private static string Publikum(KalenderabonnementDto a) => a.ErAlt ? "Alt (fullt innsyn – FIN-internt)" : $"{a.Etikett} ({a.GruppeKode})";
    private string FeedUrl(string token) => $"{Nav.BaseUri}kalender/feed/{token}.ics";

    protected override Task OnInitializedAsync() => LastAsync();

    private async Task LastAsync()
    {
        if (_laster || _arbeider || _venter is not null) return;
        var valg = ++_valg;
        _laster = true; _lastet = false; _feil = null; _status = null; _fokus = null;
        _abonnementer = [];
        try
        {
            var grupper = await Grupper.HentAktiveAsync(_levetid.Token);
            if (!Gjeldende(valg)) return;
            var abonnementer = await Feedtjeneste.HentAlleAsync(_levetid.Token);
            if (!Gjeldende(valg)) return;
            _grupper = grupper; _abonnementer = abonnementer; _lastet = true;
            if (_valgtKode != AltPublikum && !_grupper.Any(g => g.Kode == _valgtKode)) _valgtKode = "";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception)
        {
            if (Gjeldende(valg)) Feil("Kunne ikke hente abonnementene. Last på nytt for å prøve igjen.");
        }
        finally { if (Gjeldende(valg)) _laster = false; }
    }

    private void BeOmOppretting()
    {
        if (Laast || _venter is not null || _valgtKode == "" || (_valgtKode != AltPublikum && !_grupper.Any(g => g.Kode == _valgtKode))) return;
        Aapne(new(Handling.Opprett, Guid.Empty, _valgtKode == AltPublikum ? null : _valgtKode, ValgtPublikum));
    }

    private void BeOmEndring(KalenderabonnementDto rad, Handling type)
    {
        if (Laast || _venter is not null || !_abonnementer.Any(a => a.Id == rad.Id)) return;
        Aapne(new(type, rad.Id, rad.GruppeKode, Publikum(rad)));
    }

    private void Aapne(Bekreftelse bekreftelse)
    {
        ++_valg; _venter = bekreftelse; _aapneDialog = true;
        _feil = null; _status = null; _fokus = null;
    }

    private async Task BekreftAsync()
    {
        if (_arbeider || _venter is not { } vent || _modul is null) return;
        var valg = _valg;
        _arbeider = true;
        try
        {
            if (!await _modul.InvokeAsync<bool>("kanBekrefte", _dialog, valg))
            {
                _arbeider = false;
                if (Gjeldende(valg)) await AvbrytAsync();
                return;
            }
            if (!Gjeldende(valg)) return;
            KalenderabonnementDto? opprettet = null;
            switch (vent.Type)
            {
                case Handling.Opprett:
                    var identitet = await AuthState.GetAuthenticationStateAsync();
                    if (!Gjeldende(valg)) return;
                    opprettet = await Feedtjeneste.OpprettAsync(vent.Gruppekode, identitet.User.Identity?.Name ?? "administrator", _levetid.Token);
                    break;
                case Handling.SkruAv:
                case Handling.SkruPaa:
                    await Feedtjeneste.SettAktivAsync(vent.Id, vent.Type == Handling.SkruPaa, _levetid.Token);
                    break;
                case Handling.Slett:
                    await Feedtjeneste.SlettAsync(vent.Id, _levetid.Token);
                    break;
            }
            if (!Gjeldende(valg)) return;
            // Kvitter etter lagring, uten at en ny lesefeil kan skjule en fullført mutasjon.
            _abonnementer = vent.Type switch
            {
                Handling.Opprett => new[] { opprettet! }.Concat(_abonnementer).ToArray(),
                Handling.Slett => _abonnementer.Where(a => a.Id != vent.Id).ToArray(),
                _ => _abonnementer.Select(a => a.Id == vent.Id ? a with { Aktiv = vent.Type == Handling.SkruPaa } : a).ToArray()
            };
            _status = vent.Type switch
            {
                Handling.Opprett => "Abonnementslenken er opprettet. Den hemmelige lenken finnes i oversikten.",
                Handling.SkruAv => "Lenken er skrudd av. Nye feedkall gir 404.",
                Handling.SkruPaa => "Den samme lenken er aktiv igjen. Eksisterende innehavere har tilgang på nytt.",
                _ => "Lenken er slettet permanent. Den kan ikke skrus på igjen."
            };
            _fokus = "abonnement-status";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Exception)
        {
            if (Gjeldende(valg)) Feil("Kunne ikke bekrefte lagringen. Valgt publikum er beholdt. Last på nytt for å kontrollere status, eller prøv handlingen igjen.");
        }
        finally
        {
            if (Gjeldende(valg))
            {
                _arbeider = false;
                var fokus = _fokus;
                _fokus = null;
                await LukkDialogAsync();
                if (Gjeldende(valg)) _fokus = fokus;
            }
        }
    }

    private void Feil(string melding) { _feil = melding; _fokus = "abonnement-feil"; }

    private async Task AvbrytAsync()
    {
        if (_arbeider) return;
        ++_valg;
        await LukkDialogAsync();
    }

    private async Task LukkDialogAsync()
    {
        var dialog = _dialog;
        _venter = null; _aapneDialog = false;
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", dialog);
    }

    [JSInvokable]
    public Task AvbrytFraDialog(int valg)
    {
        if (!Gjeldende(valg) || _arbeider) return Task.CompletedTask;
        ++_valg; _venter = null; _aapneDialog = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task FoerNavigasjon(LocationChangingContext kontekst)
    {
        if (_arbeider) kontekst.PreventNavigation();
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_aapneDialog && _fokus is null) return;
        var valg = _valg;
        try
        {
            _modul ??= await (_modullasting ??= Js.InvokeAsync<IJSObjectReference>("import", "./abonnement.js").AsTask());
            if (!Gjeldende(valg)) return;
            if (_aapneDialog && _venter is not null)
            {
                _aapneDialog = false;
                _referanse ??= DotNetObjectReference.Create(this);
                await _modul.InvokeVoidAsync("vis", _dialog, valg, _referanse);
            }
            if (Gjeldende(valg) && _fokus is { } fokus)
            {
                _fokus = null;
                await _modul.InvokeVoidAsync("fokuser", fokus, valg);
            }
        }
        catch (JSDisconnectedException) { }
    }

    private static string Dialogtittel(Handling type) => type switch { Handling.Opprett => "Opprett abonnementslenke?", Handling.SkruAv => "Skru av lenken?", Handling.SkruPaa => "Skru på den samme lenken?", _ => "Slett lenken permanent?" };
    private static string Bekrefttekst(Handling type) => type switch { Handling.Opprett => "Ja, opprett lenke", Handling.SkruAv => "Ja, skru av", Handling.SkruPaa => "Ja, skru på", _ => "Ja, slett permanent" };

    public async ValueTask DisposeAsync()
    {
        _avsluttet = true; ++_valg;
        _levetid.Cancel(); _levetid.Dispose(); _referanse?.Dispose();
        try
        {
            var modul = _modul ?? (_modullasting is not null ? await _modullasting : null);
            if (modul is not null) await modul.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
