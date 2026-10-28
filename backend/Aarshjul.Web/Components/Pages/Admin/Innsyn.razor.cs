using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;
using Aarshjul.Web.Komponenter;
using Aarshjul.Web.Sikkerhet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Components.Pages.Admin;

public partial class Innsyn : IDisposable
{
    [Inject] private IGruppetjeneste Grupper { get; set; } = default!;
    [Inject] private IFristlesing Lesing { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;
    [Inject] private IAuthorizationService Autorisering { get; set; } = default!;
    [Inject] private ILogger<Innsyn> Logg { get; set; } = default!;
    private readonly CancellationTokenSource _levetid = new();
    private IReadOnlyList<GruppeDto> _grupper = [];
    private IReadOnlyList<FristDto> _frister = [];
    private string _valgtKode = "";
    private string? _feil, _beskjed, _detaljFeil;
    private bool _laster, _lastet, _detaljLaster, _avsluttet;
    private long _kontekst, _detalj;
    private Fristdialog? _fristdialog;
    private string ValgtNavn => _grupper.FirstOrDefault(g => g.Kode == _valgtKode)?.Navn ?? _valgtKode;
    private bool Gjeldende(long kontekst) => !_avsluttet && kontekst == _kontekst;

    protected override Task OnInitializedAsync() => LastInnAsync();

    private Task VelgGruppeAsync(ChangeEventArgs e)
    {
        _valgtKode = e.Value?.ToString() ?? "";
        return LastInnAsync();
    }

    private async Task LastInnAsync()
    {
        // Tøm også dialogen før første await. Gamle svar hører aldri til det nye gruppevalget.
        var kontekst = ++_kontekst;
        var kode = _valgtKode;
        _frister = []; _lastet = false; _feil = null; _beskjed = null;
        _detalj++; _detaljLaster = false; _detaljFeil = null; _fristdialog = null;
        _laster = true;
        try
        {
            await KrevAdministratorAsync();
            if (!Gjeldende(kontekst)) return;
            var grupper = await Grupper.HentAktiveAsync(_levetid.Token);
            if (!Gjeldende(kontekst)) return;
            _grupper = grupper;
            if (string.IsNullOrEmpty(kode)) return;
            if (!grupper.Any(g => g.Kode == kode)) { FjernInaktivtValg(); return; }
            var frister = await Lesing.HentSynligeAsync(Synlighetskontekst.ForGruppe(kode),
                new FristFilter { InkluderHistorikk = true }, _levetid.Token);
            if (!Gjeldende(kontekst)) return;
            _frister = frister; _lastet = true;
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (UnauthorizedAccessException) { if (Gjeldende(kontekst)) _feil = "Du har ikke tilgang til gruppeinnsyn."; }
        catch (Exception e)
        {
            if (!Gjeldende(kontekst)) return;
            Logg.LogError(e, "Kunne ikke laste gruppeinnsyn.");
            _feil = "Kunne ikke hente gruppeinnsyn akkurat nå. Valget er beholdt. Prøv igjen.";
        }
        finally { if (Gjeldende(kontekst)) _laster = false; }
    }

    private void FjernInaktivtValg()
    {
        _valgtKode = ""; _frister = []; _lastet = false; _fristdialog = null; _kontekst++;
        _laster = false; _detaljLaster = false; _detaljFeil = null;
        _beskjed = "Gruppen er ikke lenger aktiv. Velg en annen gruppe for å fortsette.";
    }

    private async Task VisFristAsync(FristDto rad)
    {
        if (!_lastet || string.IsNullOrEmpty(_valgtKode) || _avsluttet) return;
        var kontekst = _kontekst;
        var detalj = ++_detalj;
        var kode = _valgtKode;
        _detaljFeil = null; _detaljLaster = true;
        try
        {
            await KrevAdministratorAsync();
            if (!Gjeldende(kontekst) || detalj != _detalj) return;
            var grupper = await Grupper.HentAktiveAsync(_levetid.Token);
            if (!Gjeldende(kontekst) || detalj != _detalj) return;
            _grupper = grupper;
            if (!grupper.Any(g => g.Kode == kode)) { FjernInaktivtValg(); return; }
            // Gjenles gjennom den samme gruppen, aldri administratorens SerAlt-kontekst.
            var frist = await Lesing.HentEnAsync(Synlighetskontekst.ForGruppe(kode), rad.Id, _levetid.Token);
            if (!Gjeldende(kontekst) || detalj != _detalj) return;
            if (frist is null)
            {
                _frister = _frister.Where(f => f.Id != rad.Id).ToList();
                _detaljFeil = "Fristen er ikke lenger tilgjengelig for denne gruppen. Oppdater innsyn for å hente utvalget på nytt.";
            }
            else if (_fristdialog is not null) await _fristdialog.VisFristAsync(frist);
        }
        catch (OperationCanceledException) when (_levetid.IsCancellationRequested) { }
        catch (UnauthorizedAccessException) { if (Gjeldende(kontekst) && detalj == _detalj) _detaljFeil = "Du har ikke tilgang til gruppeinnsyn."; }
        catch (Exception e)
        {
            if (!Gjeldende(kontekst) || detalj != _detalj) return;
            Logg.LogError(e, "Kunne ikke hente gruppens fristdetaljer.");
            _detaljFeil = "Kunne ikke hente fristdetaljene akkurat nå. Prøv å åpne fristen igjen.";
        }
        finally { if (Gjeldende(kontekst) && detalj == _detalj) _detaljLaster = false; }
    }

    private async Task KrevAdministratorAsync()
    {
        var bruker = (await AuthState.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true
            || !(await Autorisering.AuthorizeAsync(bruker, Autorisasjon.ErAdministrator)).Succeeded)
            throw new UnauthorizedAccessException();
    }

    public void Dispose()
    {
        _avsluttet = true; _kontekst++; _detalj++;
        _levetid.Cancel(); _levetid.Dispose();
    }
}
