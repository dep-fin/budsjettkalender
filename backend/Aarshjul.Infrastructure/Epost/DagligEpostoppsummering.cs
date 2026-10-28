using Aarshjul.Application.Epost;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Aarshjul.Infrastructure.Epost;

public sealed class DagligEpostoppsummering(IDbContextFactory<AppDbContext> fabrikk, TimeProvider klokke,
    IMottakertilgangskilde tilgang, IOppsummeringspost post, IOptions<DagligEpostOpsjoner> opsjoner) : IDagligEpostoppsummering
{
    public async Task<Oppsummeringsresultat> KjorAsync(CancellationToken ct = default)
    {
        var dato = Datohjelp.Idag(klokke);
        if (klokke.GetUtcNow() < Oppsummeringstid.Sendetid(dato, opsjoner.Value.Klokkeslett))
            return new(dato, true, []);
        var lenke = Kalenderlenke(opsjoner.Value.OffentligBasisadresse);
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        var abonnenter = await db.Epostabonnementer.AsNoTracking().Where(x => x.Status == "Aktiv" && (x.NyeFrister || x.VesentligeEndringer)).ToListAsync(ct);
        var resultater = new List<Mottakerresultat>();
        foreach (var abonnent in abonnenter.Where(x => x.Aktivert is not null))
        {
            resultater.Add(await BehandleAsync(db, abonnent, dato, lenke, ct));
            db.ChangeTracker.Clear();
        }
        return new(dato, false, resultater);
    }

    private async Task<Mottakerresultat> BehandleAsync(AppDbContext db, Epostabonnement abonnent, DateOnly dato, Uri lenke, CancellationToken ct)
    {
        var id = abonnent.BrukerId;
        // Et avbrudd etter vinnende overgang kan ha overlevert innhold. Aldri blind retry.
        var uavklart = await db.Epostoppsummeringer.AsNoTracking().FirstOrDefaultAsync(x => x.BrukerId == id
            && (x.Status == "UnderSending" || x.Status == "Uavklart"), ct);
        if (uavklart is not null) return new(id, uavklart.Id, "Uavklart");
        var plan = await db.Epostoppsummeringer.SingleOrDefaultAsync(x => x.BrukerId == id && x.LokalDato == dato, ct);
        if (plan?.Status == "Bekreftet") return new(id, plan.Id, "Bekreftet");
        // Planen kan ha blitt overtatt etter oppslaget over. Bare en faktisk
        // innlest Planlagt-status, med samme versjon ved UPDATE, er sendbar.
        if (plan?.Status is "UnderSending" or "Uavklart") return new(id, plan.Id, "Uavklart");
        if (plan is not null && plan.Status != "Planlagt") return new(id, plan.Id, "SamtidigKjoring");
        var start = abonnent.Aktivert!.Value.UtcTicks;
        // EV02-abonnenter har ingen aktiveringsrad. Grense 0 inkluderer bare
        // nye hendelser; migrasjonen tilbakefyller ingen tidligere frister.
        var grenser = await db.Set<Epostaktivering>().AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
        var hendelser = await Ubehandlede(db, id, abonnent, grenser?.Sekvens ?? 0, grenser?.Endringssekvens ?? 0).ToListAsync(ct);
        if (hendelser.Count == 0) return new(id, plan?.Id, "IngenHendelser");
        if (plan is null)
        {
            plan = new() { BrukerId = id, LokalDato = dato, AktivertUtcTicks = start };
            db.Epostoppsummeringer.Add(plan);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(plan).State = EntityState.Detached;
                // Bare en påvist vinner på den unike nøkkelen gjør feilen til konkurranse.
                var vinner = await db.Epostoppsummeringer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id && x.LokalDato == dato, ct);
                if (vinner is null) throw;
                return new(id, vinner.Id, vinner.Status == "Bekreftet" ? "Bekreftet" : "SamtidigKjoring");
            }
        }
        var rett = await HentTilgangAsync(id, ct);
        if (rett.Status != MottakertilgangStatus.Aktiv || rett.Synlighet is null)
            return await UtsettAsync(db, plan, rett.Status == MottakertilgangStatus.Inaktiv ? "InaktivMottaker" : "UavklartTilgang", ct);
        if (!post.Tilgjengelig) return await UtsettAsync(db, plan, "UtilgjengeligTransport", ct);
        if (Datohjelp.Idag(klokke) != dato) return await UtsettAsync(db, plan, "Dagsskifte", ct);
        plan.AktivertUtcTicks = start;
        plan.Status = "UnderSending";
        plan.Utsettelsesgrunn = null;
        plan.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.Entry(plan).State = EntityState.Detached; return new(id, plan.Id, "SamtidigKjoring"); }

        // Nytt autoritativt oppslag og nåværende synlighet etter at bare én jobb har vunnet.
        rett = await HentTilgangAsync(id, ct);
        if (rett.Status != MottakertilgangStatus.Aktiv || rett.Synlighet is null)
            return await UtsettAsync(db, plan, "UavklartTilgang", ct);
        var fristIder = hendelser.Select(x => x.FristId).ToList();
        var frister = await db.Frister.AsNoTracking().Where(x => x.Status == FristStatus.Godkjent && fristIder.Contains(x.Id))
            .FiltrerSynlige(rett.Synlighet).OrderBy(x => x.Sorteringsdag).ThenBy(x => x.Tittel).ThenBy(x => x.Id)
            .Select(x => new Fristinnhold(x.Id, x.Tittel, x.Dato, x.Datopresisjon, x.Datokvalifikator, x.Klokkeslett, x.Budsjettaar, x.Kategori, x.Loep, x.Notat)).ToListAsync(ct);
        var gjeldende = await db.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
        if (gjeldende is null || gjeldende.Status != "Aktiv" || !(gjeldende.NyeFrister || gjeldende.VesentligeEndringer)
            || gjeldende.Aktivert?.UtcTicks != start || gjeldende.Adresse != abonnent.Adresse || gjeldende.Versjon != abonnent.Versjon)
            return await UtsettAsync(db, plan, "EndretAbonnement", ct);
        if (frister.Count == 0) return await UtsettAsync(db, plan, "IngenSynligeFrister", ct);

        // Lagre nøyaktige hendelser før overlevering; sene commits forblir ubehandlet.
        var synlige = frister.Select(x => x.Id).ToHashSet();
        foreach (var hendelse in hendelser.Where(x => synlige.Contains(x.FristId)))
            plan.Hendelser.Add(new() { HendelseId = hendelse.Id });
        plan.Versjon = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        rett = await HentTilgangAsync(id, ct);
        if (rett.Status != MottakertilgangStatus.Aktiv || rett.Synlighet is null)
            return await UtsettAsync(db, plan, "UavklartTilgang", ct);
        var sisteFrister = await db.Frister.AsNoTracking().Where(x => x.Status == FristStatus.Godkjent && fristIder.Contains(x.Id))
            .FiltrerSynlige(rett.Synlighet).OrderBy(x => x.Sorteringsdag).ThenBy(x => x.Tittel).ThenBy(x => x.Id)
            .Select(x => new Fristinnhold(x.Id, x.Tittel, x.Dato, x.Datopresisjon, x.Datokvalifikator, x.Klokkeslett, x.Budsjettaar, x.Kategori, x.Loep, x.Notat)).ToListAsync(ct);
        var sisteAbonnement = await db.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
        if (sisteAbonnement?.Versjon != gjeldende.Versjon || !frister.SequenceEqual(sisteFrister) || !post.Tilgjengelig || Datohjelp.Idag(klokke) != dato)
            return await UtsettAsync(db, plan, "EndretFoerOverlevering", ct);
        var melding = LagMelding(plan.Id, id, gjeldende.Adresse, frister, hendelser, lenke);
        try { await post.SendAsync(melding, ct); }
        catch (Exception)
        {
            // Også avbrudd kan skje etter overlevering. Behold ingen leverandørtekst/innhold i status.
            plan.Status = "Uavklart"; plan.Versjon = Guid.NewGuid();
            await db.SaveChangesAsync(CancellationToken.None);
            return new(id, plan.Id, "Uavklart");
        }
        plan.Status = "Bekreftet";
        plan.BekreftetUtcTicks = klokke.GetUtcNow().UtcTicks;
        plan.Versjon = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return new(id, plan.Id, "Bekreftet", frister.Count);
    }

    private static IQueryable<NyFristHendelse> Ubehandlede(AppDbContext db, string id, Epostabonnement abonnement, long nyeStart, long endringerStart)
        => db.NyFristHendelser.AsNoTracking().Where(h =>
            (h.Type == "Ny" && abonnement.NyeFrister && h.Publiseringssekvens > nyeStart
             || h.Type == "Endret" && abonnement.VesentligeEndringer && h.Publiseringssekvens > endringerStart)
            && !db.Oppsummeringshendelser.Any(k => k.HendelseId == h.Id && k.Oppsummering.BrukerId == id
                && (k.Oppsummering.Status == "Bekreftet" || k.Oppsummering.Status == "UnderSending" || k.Oppsummering.Status == "Uavklart")));

    private async Task<Mottakertilgang> HentTilgangAsync(string id, CancellationToken ct)
    {
        try { return await tilgang.HentAsync(id, ct); }
        catch (Exception feil) when (feil is not OperationCanceledException) { return new(MottakertilgangStatus.Uavklart); }
    }

    private static async Task<Mottakerresultat> UtsettAsync(AppDbContext db, Epostoppsummering plan, string grunn, CancellationToken ct)
    {
        db.Oppsummeringshendelser.RemoveRange(plan.Hendelser);
        plan.Hendelser.Clear();
        plan.Status = "Planlagt"; plan.Utsettelsesgrunn = grunn; plan.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.Entry(plan).State = EntityState.Detached; return new(plan.BrukerId, plan.Id, "SamtidigKjoring"); }
        return new(plan.BrukerId, plan.Id, grunn);
    }

    private static Uri Kalenderlenke(string basis)
    {
        if (!Uri.TryCreate(basis, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback))
            throw new InvalidOperationException("E-post krever en betrodd HTTPS-basisadresse, eller HTTP på lokal loopback i kontrollert miljø.");
        return new Uri(uri, "/kalender");
    }

    private static Oppsummeringsmelding LagMelding(Guid planId, string id, string adresse, List<Fristinnhold> frister,
        List<NyFristHendelse> hendelser, Uri lenke)
    {
        var tekst = new StringBuilder("Fristoppdateringer i budsjettkalenderen\n\n");
        var html = new StringBuilder("<h2>Fristoppdateringer i budsjettkalenderen</h2><ul>");
        foreach (var f in frister)
        {
            var gruppe = hendelser.Where(h => h.FristId == f.Id).OrderBy(h => h.Publiseringssekvens).ToList();
            var ny = gruppe.Any(h => h.Type == "Ny");
            var linje = $"{(ny ? "Ny frist" : "Vesentlig endring")}: {f.Tittel} – {Dato(f.Dato, f.Presisjon, f.Kvalifikator, f.Tid)} – Budsjettår {f.Budsjettaar} – Kategori {f.Kategori}";
            if (!ny)
            {
                var foer = gruppe.First().FoerJson is { } json ? JsonSerializer.Deserialize<Fristtilstand>(json) : null;
                if (foer is not null)
                {
                    linje += $". Tidligere: {Dato(foer.Dato, foer.Presisjon, foer.Kvalifikator, foer.Tid)} – Budsjettår {foer.Budsjettaar} – Kategori {foer.Kategori}. Nå: {Dato(f.Dato, f.Presisjon, f.Kvalifikator, f.Tid)} – Budsjettår {f.Budsjettaar} – Kategori {f.Kategori}";
                    linje += Tekstendring("Tittel", foer.Tittel, f.Tittel)
                        + Tekstendring("Løp", foer.Loep, f.Loep) + Tekstendring("Notat", foer.Notat, f.Notat);
                }
                var forklaringer = gruppe.Where(h => h.Forklaring is not null).Select(h => h.Forklaring).Distinct();
                foreach (var forklaring in forklaringer) linje += $". Forklaring: {forklaring}";
            }
            tekst.AppendLine(linje);
            html.Append("<li>").Append(WebUtility.HtmlEncode(linje)).Append("</li>");
        }
        tekst.AppendLine().Append("Åpne kalenderen (krever innlogging): ").Append(lenke.AbsoluteUri);
        html.Append("</ul><p><a href=\"").Append(WebUtility.HtmlEncode(lenke.AbsoluteUri)).Append("\">Åpne kalenderen (krever innlogging)</a></p>");
        return new(planId, id, adresse, tekst.ToString(), html.ToString());
    }
    private static string Tekstendring(string felt, string? foer, string? etter)
        => foer == etter ? "" : $". {felt} – Tidligere: {foer ?? "(ikke angitt)"}. Nå: {etter ?? "(ikke angitt)"}";

    private static string Dato(DateOnly dato, Datopresisjon presisjon, Datokvalifikator? kvalifikator, TimeOnly? tid)
        => presisjon == Datopresisjon.Maaned
            ? $"Tentativt: {(kvalifikator?.ToString().ToLowerInvariant() is { } k ? k + " " : "")}{dato.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("nb-NO"))}"
            : dato.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + (tid is { } klokke ? $" kl. {klokke:HH:mm}" : "");
    private sealed record Fristinnhold(Guid Id, string Tittel, DateOnly Dato, Datopresisjon Presisjon, Datokvalifikator? Kvalifikator, TimeOnly? Tid, int Budsjettaar, Kategori Kategori, string? Loep, string? Notat);
}
