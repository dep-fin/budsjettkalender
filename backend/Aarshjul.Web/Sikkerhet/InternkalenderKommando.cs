using Aarshjul.Application.Internkalender;
using Aarshjul.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace Aarshjul.Web.Sikkerhet;

/// <summary>Alle interne skriv autoriseres på nytt mot kretsens innloggede identitet.</summary>
public sealed class InternkalenderKommando(
    AuthenticationStateProvider tilstand,
    IAuthorizationService autorisasjon,
    Gjeldendebrukerkilde brukerkilde,
    IInternkalender kalender,
    IGjoeremaalRegler regler)
{
    public async Task<Guid> OpprettRundeAsync(Rundetype type, int aar, bool fyllFraRegler, CancellationToken ct = default)
    {
        await KrevSbrAsync();
        var bruker = await brukerkilde.HentAsync(ct);
        return fyllFraRegler ? await kalender.GenererRundeAsync(type, aar, bruker.Navn, ct)
            : await kalender.OpprettRundeAsync(type, aar, bruker.Navn, ct);
    }

    public async Task<Guid> AapneOvrigAsync(CancellationToken ct = default)
    {
        await KrevSbrAsync();
        var bruker = await brukerkilde.HentAsync(ct);
        return await kalender.HentEllerOpprettOvrigAsync(bruker.Navn, ct);
    }

    public async Task SlettRundeAsync(Guid id, CancellationToken ct = default) { await KrevSbrAsync(); await kalender.SlettRundeAsync(id, ct); }
    public async Task<Guid> HurtiglaggAsync(Guid rundeId, string tittel, CancellationToken ct = default) { await KrevSbrAsync(); return await kalender.HurtiglaggAsync(rundeId, tittel, ct); }
    public async Task<Guid> OpprettGjoeremaalAsync(Guid rundeId, GjoeremaalInndata inndata, CancellationToken ct = default) { await KrevSbrAsync(); return await kalender.OpprettGjoeremaalAsync(rundeId, inndata, ct); }
    public async Task OppdaterGjoeremaalAsync(Guid id, GjoeremaalInndata inndata, CancellationToken ct = default) { await KrevSbrAsync(); await kalender.OppdaterGjoeremaalAsync(id, inndata, ct); }
    public async Task SlettGjoeremaalAsync(Guid id, CancellationToken ct = default) { await KrevSbrAsync(); await kalender.SlettGjoeremaalAsync(id, ct); }
    public async Task FullfoerAsync(Guid id, CancellationToken ct = default)
    {
        await KrevSbrAsync();
        var bruker = await brukerkilde.HentAsync(ct);
        await kalender.FullfoerAsync(id, bruker.Id, bruker.Navn, ct);
    }
    public async Task GjenaapneAsync(Guid id, CancellationToken ct = default) { await KrevSbrAsync(); await kalender.GjenaapneAsync(id, ct); }
    public async Task SynkroniserAsync(Guid id, IReadOnlyList<SynkForslagDto> forslag, CancellationToken ct = default) { await KrevSbrAsync(); await kalender.SynkroniserAsync(id, forslag, ct); }
    public async Task<Guid> OpprettRegelAsync(RegelInndata inndata, CancellationToken ct = default) { await KrevSbrAsync(); return await regler.OpprettAsync(inndata, ct); }
    public async Task OppdaterRegelAsync(Guid id, RegelInndata inndata, CancellationToken ct = default) { await KrevSbrAsync(); await regler.OppdaterAsync(id, inndata, ct); }
    public async Task SlettRegelAsync(Guid id, CancellationToken ct = default) { await KrevSbrAsync(); await regler.SlettAsync(id, ct); }

    private async Task KrevSbrAsync()
    {
        var bruker = (await tilstand.GetAuthenticationStateAsync()).User;
        if (bruker.Identity?.IsAuthenticated != true || !(await autorisasjon.AuthorizeAsync(bruker, Autorisasjon.ErSbr)).Succeeded)
            throw new UnauthorizedAccessException("Bare SBR kan endre internkalenderen.");
    }
}
