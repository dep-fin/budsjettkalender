using Aarshjul.Application.Epost;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Aarshjul.Infrastructure.Epost;

public sealed class DagligEpostoppsummering(IDbContextFactory<AppDbContext> fabrikk, TimeProvider klokke,
    IMottakertilgangskilde tilgang, IOppsummeringspost post, IOptions<DagligEpostOpsjoner> opsjoner) : IDagligEpostoppsummering
{
    public async Task<Oppsummeringsresultat> KjorAsync(CancellationToken ct = default)
    {
        if (!opsjoner.Value.Gyldig()) throw new InvalidOperationException("Ugyldige grenser for e-postdrift.");
        var dato = Datohjelp.Idag(klokke);
        if (klokke.GetUtcNow() < Oppsummeringstid.Sendetid(dato, opsjoner.Value.Klokkeslett))
            return new(dato, true, []);
        var lenke = Kalenderlenke(opsjoner.Value.OffentligBasisadresse);
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        if (await db.Epostdrift.AsNoTracking().AnyAsync(x => x.Stanset, ct)) return new(dato, false, []);
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
        if (uavklart is not null)
        {
            // En foreldreløs claim uten kjent starttid kan ha passert mottaket.
            if (uavklart.Status == "UnderSending" && (uavklart.ForsoekStartetUtcTicks is null
                || uavklart.ForsoekStartetUtcTicks <= (klokke.GetUtcNow() - opsjoner.Value.Kvitteringsfrist).UtcTicks))
            {
                db.Attach(uavklart); uavklart.Status = "Uavklart"; uavklart.Versjon = Guid.NewGuid();
                var forladt = await db.Epostsendeforsoek.SingleOrDefaultAsync(x => x.OppsummeringId == uavklart.Id
                    && x.Nummer == uavklart.AntallForsoek && x.Status == "UnderSending", ct);
                if (forladt is not null) { forladt.Status = "Uavklart"; forladt.AvsluttetUtcTicks = klokke.GetUtcNow().UtcTicks; }
                try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { db.Entry(uavklart).State = EntityState.Detached; }
            }
            return new(id, uavklart.Id, "Uavklart");
        }
        var plan = await db.Epostoppsummeringer.Include(x => x.Hendelser).SingleOrDefaultAsync(x => x.BrukerId == id && x.LokalDato == dato, ct);
        if (plan?.Status == "Bekreftet") return new(id, plan.Id, "Bekreftet");
        if (await db.Epostoppsummeringer.AsNoTracking().AnyAsync(x => x.BrukerId == id && x.Status == "Planlagt"
            && x.NesteOppslagUtcTicks > klokke.GetUtcNow().UtcTicks, ct)) return new(id, plan?.Id, "VenterPaaOppslag");
        // Samme forsøksbudsjett følger den eldste sikre feilen over omstart og dagsskifte.
        plan ??= await db.Epostoppsummeringer.Include(x => x.Hendelser).Where(x => x.BrukerId == id && x.Status == "Feilet").OrderBy(x => x.LokalDato).FirstOrDefaultAsync(ct);
        if (plan?.Status == "Feilet")
        {
            if (plan.AntallForsoek >= opsjoner.Value.MaksSendeforsoek) return new(id, plan.Id, "ForsoekUttomt");
            if (plan.NesteForsoekUtcTicks > klokke.GetUtcNow().UtcTicks) return new(id, plan.Id, "VenterPaaRetry");
        }
        // Også manglende adgang/transport begrenser eksterne oppslag; dette teller ikke som transportforsøk.
        if (plan?.NesteOppslagUtcTicks > klokke.GetUtcNow().UtcTicks) return new(id, plan.Id, "VenterPaaOppslag");
        // Planen kan ha blitt overtatt etter oppslaget over. Bare en faktisk
        // innlest Planlagt-status, med samme versjon ved UPDATE, er sendbar.
        if (plan?.Status is "UnderSending" or "Uavklart") return new(id, plan.Id, "Uavklart");
        if (plan is not null && plan.Status is not ("Planlagt" or "Feilet")) return new(id, plan.Id, "SamtidigKjoring");
        var start = abonnent.Aktivert!.Value.UtcTicks;
        // EV02-abonnenter har ingen aktiveringsrad. Grense 0 inkluderer bare
        // nye hendelser; migrasjonen tilbakefyller ingen tidligere frister.
        var grenser = await db.Set<Epostaktivering>().AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
        var hendelseIder = await Ubehandlede(db, id, abonnent, grenser?.Sekvens ?? 0, grenser?.Endringssekvens ?? 0)
            .Select(h => h.Id).ToListAsync(ct);
        if (hendelseIder.Count == 0) return new(id, plan?.Id, "IngenHendelser");
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
        plan.LokalDato = dato;
        plan.Status = "UnderSending";
        plan.NesteOppslagUtcTicks = null;
        plan.ForsoekStartetUtcTicks = klokke.GetUtcNow().UtcTicks;
        plan.Utsettelsesgrunn = null;
        plan.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.Entry(plan).State = EntityState.Detached; return new(id, plan.Id, "SamtidigKjoring"); }

        // Nytt autoritativt oppslag og nåværende synlighet etter at bare én jobb har vunnet.
        rett = await HentTilgangAsync(id, ct);
        if (rett.Status != MottakertilgangStatus.Aktiv || rett.Synlighet is null)
            return await UtsettAsync(db, plan, "UavklartTilgang", ct);
        var gjeldende = await db.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
        if (gjeldende is null || gjeldende.Status != "Aktiv" || !(gjeldende.NyeFrister || gjeldende.VesentligeEndringer)
            || gjeldende.Aktivert?.UtcTicks != start || gjeldende.Adresse != abonnent.Adresse || gjeldende.Versjon != abonnent.Versjon)
            return await UtsettAsync(db, plan, "EndretAbonnement", ct);
        var utvalg = Abonnementsutvalg.Les(gjeldende.UtvalgJson);
        var (frister, hendelser) = await HentInnholdAsync(db, id, hendelseIder, rett.Synlighet,
            utvalg, grenser?.Livssyklussekvens ?? 0, ct);
        if (frister.Count == 0) return await UtsettAsync(db, plan, "IngenSynligeFrister", ct);

        // Lagre nøyaktige hendelser før overlevering; sene commits forblir ubehandlet.
        var synlige = frister.Select(x => x.Id).ToHashSet();
        var aktuelle = hendelser.Where(x => synlige.Contains(x.FristId)).Select(x => x.Id).ToHashSet();
        foreach (var gammel in plan.Hendelser.Where(x => !aktuelle.Contains(x.HendelseId)).ToArray())
        { db.Oppsummeringshendelser.Remove(gammel); plan.Hendelser.Remove(gammel); }
        foreach (var hendelse in aktuelle.Where(x => !plan.Hendelser.Any(k => k.HendelseId == x)))
            plan.Hendelser.Add(new() { HendelseId = hendelse });
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        plan.AvmeldingsHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        plan.Avmeldingssekvens = grenser?.Livssyklussekvens ?? 0;
        plan.Versjon = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        rett = await HentTilgangAsync(id, ct);
        if (rett.Status != MottakertilgangStatus.Aktiv || rett.Synlighet is null)
            return await UtsettAsync(db, plan, "UavklartTilgang", ct);
        var (sisteFrister, sisteHendelser) = await HentInnholdAsync(db, id, hendelseIder, rett.Synlighet,
            utvalg, grenser?.Livssyklussekvens ?? 0, ct);
        Task overlevering;
        Epostsendeforsoek forsoek;
        await using (var grense = await Epostoverlevering.LaasAsync(db, id, ct))
        {
            var sisteAbonnement = await db.Epostabonnementer.AsNoTracking().SingleOrDefaultAsync(x => x.BrukerId == id, ct);
            if (sisteAbonnement?.Versjon != gjeldende.Versjon || sisteAbonnement.Status != "Aktiv"
                || !frister.SequenceEqual(sisteFrister)
                || !hendelser.Select(h => h.Id).SequenceEqual(sisteHendelser.Select(h => h.Id))
                || !post.Tilgjengelig || Datohjelp.Idag(klokke) != dato
                || await db.Epostdrift.AsNoTracking().AnyAsync(x => x.Stanset, ct))
            {
                await grense.DisposeAsync();
                return await UtsettAsync(db, plan, "EndretFoerOverlevering", ct);
            }
            var avmeldingslenke = new Uri(lenke, "/epostvarsling/avmeld#" + token);
            var melding = LagMelding(plan.Id, id, gjeldende.Adresse, frister, hendelser, lenke, avmeldingslenke);
            plan.AntallForsoek++;
            forsoek = new() { OppsummeringId = plan.Id, Nummer = plan.AntallForsoek, StartetUtcTicks = klokke.GetUtcNow().UtcTicks };
            db.Epostsendeforsoek.Add(forsoek);
            await db.SaveChangesAsync(ct);
            // Transporten tar eierskap synkront. Bare kvitteringen kan vente.
            // Ingen avmelding kan committe mellom denne lesingen og mottaket.
            try { overlevering = post.StartOverlevering(melding, ct).Utfall; }
            catch (SikkertUsendtException) { overlevering = Task.FromException(new SikkertUsendtException()); }
            catch (Exception) { overlevering = Task.FromException(new InvalidOperationException("Uavklart overlevering.")); }
            await grense.CommitAsync(CancellationToken.None);
        }
        try { await overlevering.WaitAsync(opsjoner.Value.Kvitteringsfrist, klokke, ct); }
        catch (SikkertUsendtException)
        {
            // Bare adapterens uttrykkelige garanti gir retry. Gamle koblinger/hash er sikkert usendte.
            var nummer = forsoek.Nummer;
            while (true)
            {
                plan.Status = "Feilet"; plan.Utsettelsesgrunn = "SikkertUsendt";
                plan.NesteForsoekUtcTicks = (klokke.GetUtcNow() + opsjoner.Value.Retrypause).UtcTicks;
                // Feilet-koblingene bevarer ubehandlet historikk gjennom opprydding.
                // Ved retry velges innhold på nytt; bare faktisk sendte hendelser blir Bekreftet.
                plan.AvmeldingsHash = null; plan.Avmeldingssekvens = null;
                forsoek.Status = "Feilet"; forsoek.AvsluttetUtcTicks = klokke.GetUtcNow().UtcTicks;
                plan.Versjon = Guid.NewGuid();
                try { await db.SaveChangesAsync(CancellationToken.None); return new(id, plan.Id, "Feilet"); }
                catch (DbUpdateConcurrencyException)
                {
                    // Observasjonen kan ha utløpt fra claim-start mens dette forsøket
                    // fortsatt ventet på transportens eget, entydig sikkert-usendte utfall.
                    await db.Entry(plan).ReloadAsync(CancellationToken.None);
                    await db.Entry(forsoek).ReloadAsync(CancellationToken.None);
                    if (db.Entry(plan).State == EntityState.Detached || db.Entry(forsoek).State == EntityState.Detached
                        || plan.AntallForsoek != nummer || forsoek.Nummer != nummer || forsoek.OppsummeringId != plan.Id
                        || plan.Status != "Uavklart" || forsoek.Status != "Uavklart")
                        return new(id, plan.Id, plan.Status == "Bekreftet" ? "Bekreftet" : "Uavklart");
                    // Bare samme lagrede plan og forsøksidentitet kan avklares.
                    // Reload beholder primærnøklene; aksept eller et nyere forsøk overskrives aldri.
                }
            }
        }
        catch (Exception)
        {
            _ = overlevering.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            // Også avbrudd kan skje etter overlevering. Behold ingen leverandørtekst/innhold i status.
            plan.Status = "Uavklart"; plan.Versjon = Guid.NewGuid();
            forsoek.Status = "Uavklart"; forsoek.AvsluttetUtcTicks = klokke.GetUtcNow().UtcTicks;
            try { await db.SaveChangesAsync(CancellationToken.None); }
            catch (DbUpdateConcurrencyException) { /* En annen instans har allerede bevart Uavklart. */ }
            return new(id, plan.Id, "Uavklart");
        }
        plan.Status = "Bekreftet";
        plan.NesteForsoekUtcTicks = null;
        forsoek.Status = "Bekreftet"; forsoek.AvsluttetUtcTicks = klokke.GetUtcNow().UtcTicks;
        plan.BekreftetUtcTicks = klokke.GetUtcNow().UtcTicks;
        plan.Versjon = Guid.NewGuid();
        try { await db.SaveChangesAsync(CancellationToken.None); }
        catch (DbUpdateConcurrencyException)
        {
            // En konservativ omstartsobservasjon kan ha markert samme forsøk uavklart.
            // En positiv kvittering for nettopp dette forsøket kan avklare det senere.
            await db.Entry(plan).ReloadAsync(CancellationToken.None);
            if (plan.Status != "Uavklart" || plan.AntallForsoek != forsoek.Nummer) return new(id, plan.Id, "Uavklart");
            await db.Entry(forsoek).ReloadAsync(CancellationToken.None);
            forsoek.Status = "Bekreftet"; forsoek.AvsluttetUtcTicks = klokke.GetUtcNow().UtcTicks;
            plan.Status = "Bekreftet"; plan.BekreftetUtcTicks = klokke.GetUtcNow().UtcTicks; plan.Versjon = Guid.NewGuid();
            await db.SaveChangesAsync(CancellationToken.None);
        }
        return new(id, plan.Id, "Bekreftet", frister.Count);
    }

    private static IQueryable<NyFristHendelse> Ubehandlede(AppDbContext db, string id, Epostabonnement abonnement, long nyeStart, long endringerStart)
        => db.NyFristHendelser.AsNoTracking().Where(h =>
            (h.Type == "Ny" && abonnement.NyeFrister && h.Publiseringssekvens > nyeStart
             || (h.Type == "Endret" || h.Type == "Slettet") && abonnement.VesentligeEndringer && h.Publiseringssekvens > endringerStart)
            && !db.Oppsummeringshendelser.Any(k => k.HendelseId == h.Id && k.Oppsummering.BrukerId == id
                && (k.Oppsummering.Status == "Bekreftet" || k.Oppsummering.Status == "UnderSending" || k.Oppsummering.Status == "Uavklart")));

    private static bool Relevant(NyFristHendelse h, Fristinnhold gjeldende, Abonnementsutvalg utvalg)
    {
        bool Treffer(string? json)
        {
            var tilstand = json is null ? null : JsonSerializer.Deserialize<Fristtilstand>(json);
            return tilstand is not null && utvalg.Treffer(h.Publiseringssekvens, tilstand.Budsjettaar, tilstand.Kategori);
        }
        // EV03 registrerte Ny uten metadata. Bare dagens allerede autoriserte frist
        // kan brukes som fallback; ingen førtilstand eller historikk konstrueres.
        if (h.Type == "Ny" && h.EtterJson is null)
            return utvalg.Treffer(h.Publiseringssekvens, gjeldende.Budsjettaar, gjeldende.Kategori);
        return h.Type == "Slettet" ? Treffer(h.FoerJson) : Treffer(h.EtterJson) || h.Type == "Endret" && Treffer(h.FoerJson);
    }

    private static async Task<(List<Fristinnhold> Frister, List<NyFristHendelse> Hendelser)> HentInnholdAsync(
        AppDbContext db, string brukerId, List<Guid> hendelseIder, ISynlighetskontekst rett,
        Abonnementsutvalg utvalg, long livssyklusstart, CancellationToken ct)
    {
        var fristIder = db.NyFristHendelser.Where(h => hendelseIder.Contains(h.Id)).Select(h => h.FristId);
        var frister = await db.Frister.AsNoTracking().Where(f => f.Status == FristStatus.Godkjent && fristIder.Contains(f.Id))
            .FiltrerSynlige(rett)
            .Select(f => new Fristinnhold(f.Id, f.Tittel, f.Dato, f.Datopresisjon, f.Datokvalifikator,
                f.Klokkeslett, f.Budsjettaar, f.Kategori, f.Loep, f.Notat, false, f.ManuellKildeUrl)).ToListAsync(ct);
        var synligeIder = frister.Select(f => f.Id).ToList(); var grupper = rett.Grupper.ToList();
        // Historisk metadata filtreres i SQL. Bare eksplisitt atomisk Slettet kan
        // bruke siste publiserte synlighet; en manglende frist er ingen fallback.
        var hendelser = await db.NyFristHendelser.AsNoTracking().Where(h => hendelseIder.Contains(h.Id)
            && (h.Type == "Slettet"
                ? !db.Frister.Any(f => f.Id == h.FristId && f.Status == FristStatus.Godkjent)
                  && !db.NyFristHendelser.Any(t => t.FristId == h.FristId && t.Publiseringssekvens > h.Publiseringssekvens)
                  && (rett.SerAlt || db.Set<FristHendelsessynlighet>().Any(s => s.HendelseId == h.Id && grupper.Contains(s.GruppeKode)))
                  && !db.NyFristHendelser.Any(n => n.FristId == h.FristId && n.Type == "Ny"
                      && n.Publiseringssekvens > livssyklusstart && n.Publiseringssekvens <= h.Publiseringssekvens
                      && !db.Oppsummeringshendelser.Any(k => k.Oppsummering.BrukerId == brukerId && k.Oppsummering.Status == "Bekreftet"
                          && db.NyFristHendelser.Any(t => t.Id == k.HendelseId && t.FristId == h.FristId)))
                : synligeIder.Contains(h.FristId)))
            .OrderBy(h => h.Publiseringssekvens).ThenBy(h => h.Id).ToListAsync(ct);
        // Bare siste bortfall beskrives. Endret-metadata følger aldri med en
        // sletting når dagens publiserte frist ikke lenger gir lesetilgang.
        var slettinger = hendelser.Where(h => h.Type == "Slettet").GroupBy(h => h.FristId).Select(g => g.Last()).ToList();
        foreach (var h in slettinger)
            if (h.FoerJson is { } json && JsonSerializer.Deserialize<Fristtilstand>(json) is { } f)
                frister.Add(new(h.FristId, f.Tittel, f.Dato, f.Presisjon, f.Kvalifikator, f.Tid, f.Budsjettaar, f.Kategori, f.Loep, f.Notat, true, f.ManuellKildeUrl));
        var autoriserte = frister.ToDictionary(f => f.Id);
        hendelser = hendelser.Where(h => (h.Type != "Slettet" || slettinger.Contains(h))
            && autoriserte.TryGetValue(h.FristId, out var f) && Relevant(h, f, utvalg)).ToList();
        var relevante = hendelser.Select(h => h.FristId).ToHashSet();
        return (frister.Where(f => relevante.Contains(f.Id))
            .OrderBy(f => Datoberegning.Sorteringsdag(f.Dato, f.Presisjon, f.Kvalifikator))
            .ThenBy(f => f.Tittel).ThenBy(f => f.Id).ToList(), hendelser);
    }

    private async Task<Mottakertilgang> HentTilgangAsync(string id, CancellationToken ct)
    {
        using var avbrudd = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try { return await tilgang.HentAsync(id, avbrudd.Token).WaitAsync(opsjoner.Value.Tilgangsfrist, klokke, ct); }
        catch (TimeoutException) { avbrudd.Cancel(); return new(MottakertilgangStatus.Uavklart); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(MottakertilgangStatus.Uavklart); }
        catch (Exception feil) when (feil is not OperationCanceledException) { return new(MottakertilgangStatus.Uavklart); }
    }

    private async Task<Mottakerresultat> UtsettAsync(AppDbContext db, Epostoppsummering plan, string grunn, CancellationToken ct)
    {
        if (plan.AntallForsoek == 0)
        {
            db.Oppsummeringshendelser.RemoveRange(plan.Hendelser);
            plan.Hendelser.Clear();
        }
        plan.Status = "Planlagt"; plan.Utsettelsesgrunn = grunn; plan.Versjon = Guid.NewGuid();
        // En utsatt retry beholder både budsjettet og den sikre feilen over nye dager.
        if (plan.AntallForsoek > 0) plan.Status = "Feilet";
        plan.ForsoekStartetUtcTicks = null;
        plan.NesteOppslagUtcTicks = (klokke.GetUtcNow() + opsjoner.Value.Oppslagspause).UtcTicks;
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
        List<NyFristHendelse> hendelser, Uri lenke, Uri avmeldingslenke)
    {
        var tekst = new StringBuilder("Fristoppdateringer i budsjettkalenderen\n\n");
        var html = new StringBuilder("<h2>Fristoppdateringer i budsjettkalenderen</h2><ul>");
        foreach (var f in frister)
        {
            var gruppe = hendelser.Where(h => h.FristId == f.Id).OrderBy(h => h.Publiseringssekvens).ToList();
            var ny = gruppe.Any(h => h.Type == "Ny");
            var linje = $"{(f.Slettet ? "Fjernet frist" : ny ? "Ny frist" : "Vesentlig endring")}: {f.Tittel} – {Dato(f.Dato, f.Presisjon, f.Kvalifikator, f.Tid)} – Budsjettår {f.Budsjettaar} – Kategori {f.Kategori}";
            if (!ny && !f.Slettet)
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
            if (f.Slettet)
            {
                // Bare et lite kalenderutvalg; ingen tittel, notat, ID eller token.
                var query = $"?aar={f.Budsjettaar}&kategori={(int)f.Kategori}&dato={f.Dato:yyyy-MM-dd}&fjernet=1";
                var fjernetlenke = new Uri(lenke, query).AbsoluteUri;
                tekst.AppendLine("Åpne relevant kalenderutvalg (krever innlogging): " + fjernetlenke);
                html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(fjernetlenke))
                    .Append("\">Åpne relevant kalenderutvalg (krever innlogging)</a></p>");
            }
        }
        tekst.AppendLine().Append("Åpne kalenderen (krever innlogging): ").Append(lenke.AbsoluteUri);
        html.Append("</ul><p><a href=\"").Append(WebUtility.HtmlEncode(lenke.AbsoluteUri)).Append("\">Åpne kalenderen (krever innlogging)</a></p>");
        tekst.AppendLine().AppendLine().Append("Meld deg av e-postvarsling: ").Append(avmeldingslenke.AbsoluteUri);
        html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(avmeldingslenke.AbsoluteUri)).Append("\">Meld deg av e-postvarsling</a></p>");
        return new(planId, id, adresse, tekst.ToString(), html.ToString());
    }
    private static string Tekstendring(string felt, string? foer, string? etter)
        => foer == etter ? "" : $". {felt} – Tidligere: {foer ?? "(ikke angitt)"}. Nå: {etter ?? "(ikke angitt)"}";

    private static string Dato(DateOnly dato, Datopresisjon presisjon, Datokvalifikator? kvalifikator, TimeOnly? tid)
        => presisjon == Datopresisjon.Maaned
            ? $"Tentativt: {(kvalifikator?.ToString().ToLowerInvariant() is { } k ? k + " " : "")}{dato.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("nb-NO"))}"
            : dato.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + (tid is { } klokke ? $" kl. {klokke:HH:mm}" : "");
    private sealed record Fristinnhold(Guid Id, string Tittel, DateOnly Dato, Datopresisjon Presisjon, Datokvalifikator? Kvalifikator, TimeOnly? Tid, int Budsjettaar, Kategori Kategori, string? Loep, string? Notat, bool Slettet, string? ManuellKildeUrl);
}
