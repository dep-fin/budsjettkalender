using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Generering;
using Aarshjul.Web.Api;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class OverfoerMal : IAsyncDisposable
{
    [Inject] private MaloverfoeringKommando Kommando { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<OverfoerMal> Logg { get; set; } = default!;
    private const long MaksStorrelse = 25 * 1024 * 1024;
    private const string ImportVentestatus = "Vent til importen er ferdig før du forlater siden.";
    private readonly CancellationTokenSource _levetid = new();
    private MalDatabase? _database;
    private MalImportForhaandsvisning? _preview;
    private ImportResultat? _resultat;
    private string? _navn, _feil, _status, _fokus;
    private int _valg, _filfelt;
    private bool _leser, _kontrollerer, _lagrer, _bekreftelse, _aapneDialog, _lagringsfeil, _avsluttet;
    private ElementReference _dialog;
    private IJSObjectReference? _modul;
    private Task<IJSObjectReference>? _modulIndlaesning;
    private DotNetObjectReference<OverfoerMal>? _referanse;
    private bool Gjeldende(int valg) => !_avsluttet && valg == _valg;

    private async Task VelgFilAsync(InputFileChangeEventArgs e)
    {
        if (_lagrer) return;
        var valg = ++_valg;
        _database = null; _preview = null; _resultat = null;
        _bekreftelse = false; _aapneDialog = false; _kontrollerer = false;
        _feil = null; _status = null; _fokus = null; _lagringsfeil = false;
        var fil = e.File;
        _navn = fil.Name;
        _leser = true;
        try
        {
            if (fil.Size > MaksStorrelse) throw new Valideringsfeil("Filen er for stor (maks 25 MB). Velg en mindre JSON-fil.");
            if (!fil.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new Valideringsfeil("Kun JSON-filer støttes. Velg en malfil med filendelsen .json.");
            await using var stroem = fil.OpenReadStream(MaksStorrelse, _levetid.Token);
            var database = await JsonSerializer.DeserializeAsync<MalDatabase>(stroem, FristEndepunkter.DatabaseJson, _levetid.Token)
                ?? throw new Valideringsfeil("Fila er tom eller inneholder ikke en mal. Velg en annen JSON-fil.");
            if (!Gjeldende(valg)) return;
            var preview = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _database = database; _preview = preview; _fokus = "malimport-preview";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (JsonException) { Filfeil(valg, "Klarte ikke tolke JSON-fila. Kontroller at den er en fullstendig malfil, og velg fila på nytt. Den tidligere årsmalen er beholdt."); }
        catch (Valideringsfeil e2) { Filfeil(valg, $"{e2.Message} Den tidligere årsmalen er beholdt."); }
        catch (Exception e2)
        {
            if (!Gjeldende(valg)) return;
            Logg.LogError(e2, "Kunne ikke lese eller forhåndsvise malfil.");
            Filfeil(valg, "Kunne ikke lese og kontrollere fila akkurat nå. Velg fila på nytt. Den tidligere årsmalen er beholdt.");
        }
        finally { if (Gjeldende(valg)) _leser = false; }
    }

    private void Filfeil(int valg, string melding)
    {
        if (!Gjeldende(valg)) return;
        _database = null; _preview = null; _bekreftelse = false;
        _feil = melding; _fokus = "malimport-feil";
    }

    private async Task BeOmBekreftelseAsync()
    {
        if (_database is null || _preview is null || _leser || _kontrollerer || _lagrer) return;
        var valg = _valg;
        var database = _database;
        _kontrollerer = true; _feil = null; _status = null; _fokus = null; _lagringsfeil = false;
        try
        {
            var preview = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            _preview = preview; _bekreftelse = true; _aapneDialog = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { Filfeil(valg, e.Message); }
        catch (Exception e) { if (Gjeldende(valg)) { Logg.LogError(e, "Kunne ikke oppdatere malimportens konsekvens."); _feil = "Kunne ikke oppdatere forhåndsvisningen akkurat nå. Ingen regler er endret. Prøv igjen."; _fokus = "malimport-feil"; } }
        finally { if (Gjeldende(valg)) _kontrollerer = false; }
    }

    private async Task ImporterAsync()
    {
        if (_lagrer || !_bekreftelse || _database is null || _preview is null || _modul is null) return;
        var valg = _valg;
        var database = _database;
        var antall = _preview.EksisterendeAntall;
        _lagrer = true; _feil = null;
        try
        {
            // Escape kan ha lukket den native dialogen før kretsen mottar avbryt-hendelsen.
            if (!await _modul.InvokeAsync<bool>("kanBekrefte", _dialog, valg))
            {
                _lagrer = false;
                if (Gjeldende(valg)) await AvbrytAsync();
                return;
            }
            if (!Gjeldende(valg)) return;
            var oppdatert = await Kommando.ForhaandsvisAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            if (oppdatert.EksisterendeAntall != antall)
            {
                _preview = oppdatert;
                await LukkDialogAsync();
                _status = "Antallet lagrede regler er endret. Konsekvensen er oppdatert; kontroller den og bekreft på nytt.";
                _fokus = "malimport-preview";
                return;
            }
            var resultat = await Kommando.ImporterAsync(database, _levetid.Token);
            if (!Gjeldende(valg)) return;
            await LukkDialogAsync();
            _resultat = resultat; _database = null; _preview = null; _filfelt++;
            _fokus = "malimport-kvittering";
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (Valideringsfeil e) { await LukkDialogAsync(); Filfeil(valg, e.Message); }
        catch (Exception e)
        {
            if (!Gjeldende(valg)) return;
            Logg.LogError(e, "Kunne ikke lagre importert årsmal.");
            await LukkDialogAsync();
            _lagringsfeil = true;
            _feil = "Importen kunne ikke lagres. Den tidligere årsmalen er beholdt. Fil og forhåndsvisning er beholdt; prøv igjen og bekreft på nytt.";
            _fokus = "malimport-feil";
        }
        finally
        {
            if (Gjeldende(valg))
            {
                _lagrer = false;
                if (_status == ImportVentestatus) _status = null;
            }
        }
    }

    private async Task LukkDialogAsync()
    {
        _bekreftelse = false; _aapneDialog = false;
        if (_modul is not null) await _modul.InvokeVoidAsync("lukk", _dialog);
    }

    private async Task AvbrytAsync()
    {
        if (_lagrer) return;
        var dialog = _dialog;
        // Ugyldiggjør synkront, før eventuell JS-venting kan slippe inn en ny filhendelse.
        ++_valg; ++_filfelt;
        _bekreftelse = false; _aapneDialog = false;
        _database = null; _preview = null; _resultat = null; _navn = null; _feil = null;
        _leser = false; _kontrollerer = false; _lagringsfeil = false;
        _status = "Maloverføringen er avbrutt. Ingen regler er endret. Velg en fil for å begynne på nytt.";
        _fokus = "malfil";
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
        if (_lagrer) { kontekst.PreventNavigation(); _status = ImportVentestatus; return InvokeAsync(StateHasChanged); }
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
                await _modul.InvokeVoidAsync("fokuser", fokus);
            }
        }
        catch (JSDisconnectedException) { }
    }

    private static string Navn(MalRegelEksport regel) => !string.IsNullOrWhiteSpace(regel.Tittel) ? regel.Tittel : !string.IsNullOrWhiteSpace(regel.Loep) ? regel.Loep : "Regel uten løp";
    private static string Beskriv(MalRegelEksport regel) => Malregelutkast.Beskriv(new MalRegelDto(regel.Id, regel.Loep, regel.Tittel, regel.Kategori, regel.Regeltype, regel.Regelparametre, regel.Valgaarssensitiv));

    public async ValueTask DisposeAsync()
    {
        _avsluttet = true; ++_valg;
        _levetid.Cancel(); _levetid.Dispose();
        _referanse?.Dispose();
        try
        {
            var modul = _modul ?? (_modulIndlaesning is not null ? await _modulIndlaesning : null);
            if (modul is not null) await modul.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
