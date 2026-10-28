using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Web.Api;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Data : IAsyncDisposable
{
    [Inject] private FristoverfoeringKommando Kommando { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    private const long MaksStorrelse = 25 * 1024 * 1024;
    private const string Ventestatus = "Vent til importen er ferdig før du forlater siden.";
    private readonly CancellationTokenSource _levetid = new();
    private IBrowserFile? _fil;
    private FristDatabase? _database;
    private FristImportForhaandsvisning? _preview;
    private ImportResultat? _resultat;
    private string? _navn, _storrelse, _feil, _status, _fokus;
    private int _valg, _filfelt;
    private bool _leser, _kontrollerer, _lagrer, _bekreftelse, _aapneDialog, _avsluttet;
    private ElementReference _dialog, _flate;
    private IJSObjectReference? _modul;
    private Task<IJSObjectReference>? _modulIndlaesning;
    private DotNetObjectReference<Data>? _referanse;
    private bool Gjeldende(int valg) => !_avsluttet && valg == _valg;

    private void VelgFil(InputFileChangeEventArgs e)
    {
        if (_lagrer) return;
        ++_valg;
        _fil = e.File; _navn = e.File.Name; _storrelse = $"{e.File.Size / 1024} kB";
        _database = null; _preview = null; _resultat = null;
        _bekreftelse = false; _aapneDialog = false; _leser = false; _kontrollerer = false;
        _feil = null; _status = null; _fokus = null;
        if (e.File.Size > MaksStorrelse) Filfeil(_valg, "Filen er for stor (maks 25 MB). Velg en mindre JSON-fil.");
        else if (!e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) Filfeil(_valg, "Kun JSON-filer støttes. Velg en fristfil med filendelsen .json.");
    }

    private async Task ForhaandsvisAsync()
    {
        if (_fil is null || _leser || _kontrollerer || _lagrer) return;
        var valg = ++_valg;
        var fil = _fil;
        _leser = true; _feil = null; _status = null; _resultat = null;
        _database = null; _preview = null; _bekreftelse = false; _aapneDialog = false;
        try
        {
            await using var stroem = fil.OpenReadStream(MaksStorrelse, _levetid.Token);
            var database = await JsonSerializer.DeserializeAsync<FristDatabase>(stroem, FristEndepunkter.DatabaseJson, _levetid.Token)
                ?? throw new Valideringsfeil("Fila er tom eller inneholder ikke en fristdatabase.");
            if (!Gjeldende(valg)) return;
            var preview = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _database = database; _preview = preview; _fokus = "fristimport-preview";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (JsonException) { Filfeil(valg, "Klarte ikke tolke JSON-fila. Kontroller at den inneholder en fullstendig fristdatabase, og velg fila på nytt."); }
        catch (Valideringsfeil e) { Filfeil(valg, e.Message); }
        catch (Exception) { if (Gjeldende(valg)) { _feil = "Kunne ikke lese og kontrollere fila akkurat nå. Ingen frister er endret. Prøv igjen."; _fokus = "fristimport-feil"; } }
        finally { if (Gjeldende(valg)) _leser = false; }
    }

    private void Filfeil(int valg, string melding)
    {
        if (!Gjeldende(valg)) return;
        _database = null; _preview = null; _fil = null;
        _bekreftelse = false; _aapneDialog = false;
        _feil = $"{melding} Ingen frister er endret."; _fokus = "fristimport-feil";
    }

    private async Task BeOmBekreftelseAsync()
    {
        if (_database is null || _preview is null || _leser || _kontrollerer || _lagrer) return;
        var valg = _valg;
        var database = _database;
        _kontrollerer = true; _feil = null; _status = null; _fokus = null;
        try
        {
            var preview = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _preview = preview; _bekreftelse = true; _aapneDialog = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { Filfeil(valg, e.Message); }
        catch (Exception) { if (Gjeldende(valg)) { _feil = "Kunne ikke oppdatere importens konsekvens. Ingen frister er endret. Prøv igjen."; _fokus = "fristimport-feil"; } }
        finally { if (Gjeldende(valg)) _kontrollerer = false; }
    }

    private async Task ImporterAsync(int forventetValg)
    {
        if (!Gjeldende(forventetValg) || _lagrer || !_bekreftelse || _database is null || _preview is null || _modul is null) return;
        var valg = _valg;
        var database = _database;
        var bekreftet = _preview;
        _lagrer = true; _feil = null;
        try
        {
            if (!await _modul.InvokeAsync<bool>("kanBekrefte", _dialog, valg))
            {
                _lagrer = false;
                if (Gjeldende(valg)) await AvbrytAsync();
                return;
            }
            if (!Gjeldende(valg)) return;
            var oppdatert = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            if (!SammeKonsekvens(bekreftet, oppdatert))
            {
                _preview = oppdatert;
                await LukkDialogAsync();
                _status = "Lagrede frister eller grupper er endret. Konsekvensen er oppdatert; kontroller den og bekreft på nytt.";
                _fokus = "fristimport-preview";
                return;
            }
            var resultat = await Kommando.ImporterAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _resultat = resultat; _database = null; _preview = null; _fil = null; ++_filfelt;
            await LukkDialogAsync();
            _fokus = "fristimport-kvittering";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { await LukkDialogAsync(); Filfeil(valg, e.Message); }
        catch (Exception)
        {
            if (!Gjeldende(valg)) return;
            await LukkDialogAsync();
            if (_resultat is null)
            {
                _feil = "Importen kunne ikke lagres. Den tidligere fristdatabasen er beholdt. Fil og forhåndsvisning er beholdt; prøv igjen og bekreft på nytt.";
                _fokus = "fristimport-feil";
            }
        }
        finally
        {
            if (Gjeldende(valg))
            {
                _lagrer = false;
                if (_status == Ventestatus) _status = null;
            }
        }
    }

    private static bool SammeKonsekvens(FristImportForhaandsvisning a, FristImportForhaandsvisning b)
        => a.EksisterendeAntall == b.EksisterendeAntall && a.Advarsler.SequenceEqual(b.Advarsler)
           && a.Frister.Count == b.Frister.Count
           && a.Frister.Zip(b.Frister).All(par => par.First.SynligFor.SequenceEqual(par.Second.SynligFor));

    private async Task LukkDialogAsync()
    {
        _bekreftelse = false; _aapneDialog = false;
        try { if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _dialog); }
        catch (JSDisconnectedException) { }
    }

    private async Task AvbrytAsync()
    {
        if (_lagrer) return;
        var dialog = _dialog;
        ++_valg; ++_filfelt;
        _fil = null; _database = null; _preview = null; _resultat = null; _navn = null; _storrelse = null;
        _leser = false; _kontrollerer = false; _bekreftelse = false; _aapneDialog = false; _feil = null;
        _status = "Fristoverføringen er avbrutt. Ingen frister er endret. Velg en fil for å begynne på nytt.";
        _fokus = "fristfil";
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", dialog);
    }

    [JSInvokable]
    public async Task AvbrytFraDialog(int valg)
    {
        if (!Gjeldende(valg) || _lagrer) return;
        await AvbrytAsync();
        StateHasChanged();
    }

    private Task FoerNavigasjon(LocationChangingContext kontekst)
    {
        if (_lagrer) { kontekst.PreventNavigation(); _status = Ventestatus; return InvokeAsync(StateHasChanged); }
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_aapneDialog && _fokus is null) return;
        var valg = _valg;
        try
        {
            _modul ??= await (_modulIndlaesning ??= Js.InvokeAsync<IJSObjectReference>("import", "./maloverfoering.js").AsTask());
            if (!Gjeldende(valg)) return;
            if (_aapneDialog && _bekreftelse)
            {
                _aapneDialog = false;
                _referanse ??= DotNetObjectReference.Create(this);
                await _modul.InvokeVoidAsync("vis", _dialog, valg, _referanse);
            }
            if (Gjeldende(valg) && _fokus is { } fokus)
            {
                _fokus = null;
                await using var fokusmodul = await Js.InvokeAsync<IJSObjectReference>("import", "./eksportfokus.js");
                if (Gjeldende(valg)) await fokusmodul.InvokeVoidAsync("fokuser", _flate, valg, fokus);
            }
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _avsluttet = true; ++_valg;
        _levetid.Cancel(); _levetid.Dispose(); _referanse?.Dispose();
        try
        {
            var modul = _modul ?? (_modulIndlaesning is not null ? await _modulIndlaesning : null);
            if (modul is not null) await modul.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
