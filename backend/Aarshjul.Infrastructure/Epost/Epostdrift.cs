using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aarshjul.Infrastructure.Epost;

public sealed class Epostdrift(IDbContextFactory<AppDbContext> fabrikk, TimeProvider klokke, IOptions<DagligEpostOpsjoner> opsjoner) : IEpostdrift
{
    public async Task<EpostdriftDto> HentAsync(CancellationToken ct = default)
    {
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var rad = await db.Epostdrift.AsNoTracking().SingleAsync(ct);
        var antall = await db.Epostoppsummeringer.AsNoTracking().GroupBy(x => x.Status)
            .Select(g => new EpoststatusAntall(g.Key, g.Count())).ToListAsync(ct);
        var forsoek = await db.Epostsendeforsoek.AsNoTracking().OrderByDescending(x => x.StartetUtcTicks).ThenBy(x => x.Id).Take(100)
            .Select(x => new EpostforsoekDto(x.OppsummeringId, x.Nummer, x.Status, x.StartetUtcTicks, x.AvsluttetUtcTicks)).ToListAsync(ct);
        var planer = await db.Epostoppsummeringer.AsNoTracking().Where(x => x.Status != "Bekreftet").OrderBy(x => x.LokalDato).ThenBy(x => x.Id).Take(100)
            .Select(x => new EpostplanstatusDto(x.Id, x.LokalDato, x.Status, x.Utsettelsesgrunn, x.AntallForsoek, x.NesteForsoekUtcTicks, x.NesteOppslagUtcTicks)).ToListAsync(ct);
        return new(rad.Stanset, rad.Versjon, rad.AktoerId, rad.EndretUtcTicks, antall, forsoek, planer);
    }

    public async Task SettStansAsync(bool stanset, Guid versjon, bool bekreftet, string aktoerId, CancellationToken ct = default)
    {
        if (!bekreftet || string.IsNullOrWhiteSpace(aktoerId) || aktoerId.Length > 128)
            throw new Valideringsfeil("Uttrykkelig bekreftelse og innlogget aktør kreves.");
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        await using var grense = await Epostoverlevering.LaasAsync(db, "", ct);
        var rad = await db.Epostdrift.SingleAsync(ct);
        if (rad.Versjon != versjon) throw new EpostabonnementKonflikt();
        if (rad.Stanset == stanset) return;
        rad.Stanset = stanset; rad.AktoerId = aktoerId; rad.EndretUtcTicks = klokke.GetUtcNow().UtcTicks; rad.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new EpostabonnementKonflikt(); }
        await grense.CommitAsync(ct);
    }

    public async Task<int> RyddAsync(CancellationToken ct = default)
    {
        var o = opsjoner.Value;
        if (!o.Gyldig()) throw new InvalidOperationException("Ugyldige grenser for e-postdrift.");
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        // Samme globale lås som aktivering, import og overlevering. Hele batchen rulles tilbake ved feil.
        await using var grense = await Epostoverlevering.LaasAsync(db, "", ct);
        var naa = klokke.GetUtcNow(); var antall = 0;
        // Strøm kandidatene fordi SQLite ikke kan sammenligne DateTimeOffset. Høyst én batch endres.
        await foreach (var a in db.Epostabonnementer.Where(x => x.BekreftelsesHash != null).OrderBy(x => x.BrukerId).AsAsyncEnumerable().WithCancellation(ct))
        {
            if (a.BekreftelseUtloeper is not { } utloeper || utloeper > naa) continue;
            a.BekreftelsesHash = null; a.BekreftelseUtloeper = null; a.Versjon = Guid.NewGuid(); antall++;
            // Bevar kvote, forsøksantall, status, aktivering og utvalgsgrenser.
            if (antall == o.Ryddebatch) break;
        }
        var forsoeksgrense = (naa - TimeSpan.FromDays(o.ForsoeksdetaljerDager)).UtcTicks;
        var gamle = await db.Epostsendeforsoek.Where(x => x.Status == "Bekreftet" && x.AvsluttetUtcTicks < forsoeksgrense
            && db.Epostoppsummeringer.Any(p => p.Id == x.OppsummeringId && p.Status == "Bekreftet"))
            .OrderBy(x => x.StartetUtcTicks).Take(o.Ryddebatch - antall).ToListAsync(ct);
        db.Epostsendeforsoek.RemoveRange(gamle); antall += gamle.Count;
        var gamleRetter = await db.Epostoppsummeringer.Where(p => p.Status == "Bekreftet" && p.BekreftetUtcTicks < forsoeksgrense
            && p.AvmeldingsHash != null && db.Set<Epostaktivering>().Any(g => g.BrukerId == p.BrukerId && g.Livssyklussekvens != p.Avmeldingssekvens))
            .OrderBy(p => p.BekreftetUtcTicks).ThenBy(p => p.Id).Take(o.Ryddebatch - antall).ToListAsync(ct);
        foreach (var p in gamleRetter) { p.AvmeldingsHash = null; p.Avmeldingssekvens = null; p.Versjon = Guid.NewGuid(); antall++; }
        var innholdsgrense = (naa - TimeSpan.FromDays(o.HistoriskInnholdDager)).UtcTicks;
        // Bevar Ny-identitet, siste før/ettertilstand og alle potensielt ventende/feilede/uavklarte hendelser.
        var hendelser = await db.NyFristHendelser.Where(h => h.OpprettetUtcTicks < innholdsgrense
            && (h.FoerJson != null || h.EtterJson != null || h.Forklaring != null)
            && db.NyFristHendelser.Any(n => n.FristId == h.FristId && n.Publiseringssekvens > h.Publiseringssekvens)
            && !db.Oppsummeringshendelser.Any(k => k.HendelseId == h.Id && k.Oppsummering.Status != "Bekreftet")
            && !db.Epostabonnementer.Any(a => a.Status == "Aktiv"
                && !db.Oppsummeringshendelser.Any(k => k.HendelseId == h.Id && k.Oppsummering.BrukerId == a.BrukerId && k.Oppsummering.Status == "Bekreftet")
                && (!db.Set<Epostaktivering>().Any(g => g.BrukerId == a.BrukerId)
                    || db.Set<Epostaktivering>().Any(g => g.BrukerId == a.BrukerId
                        && (h.Type == "Ny" && a.NyeFrister && h.Publiseringssekvens > g.Sekvens
                            || h.Type != "Ny" && a.VesentligeEndringer && h.Publiseringssekvens > g.Endringssekvens)))))
            .OrderBy(h => h.Publiseringssekvens).Take(o.Ryddebatch - antall).ToListAsync(ct);
        foreach (var h in hendelser) { h.FoerJson = null; h.EtterJson = null; h.Forklaring = null; antall++; }
        // Planer/dagsnøkler/koblinger og gjeldende avmeldingshash slettes aldri.
        if (antall > 0) await db.SaveChangesAsync(ct);
        await grense.CommitAsync(ct); return antall;
    }
}
