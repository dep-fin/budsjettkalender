using System.Globalization;
using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Epost;

public sealed class Epostabonnentadministrasjon(IDbContextFactory<AppDbContext> fabrikk, TimeProvider klokke)
    : IEpostabonnentadministrasjon
{
    public async Task<IReadOnlyList<EpostabonnentDto>> HentAsync(CancellationToken ct = default)
    {
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        // Start i abonnementene. Manglende lokal bruker beholder stabil identitet som navn.
        var rader = await (from a in db.Epostabonnementer.AsNoTracking()
                           join b in db.Brukere.AsNoTracking() on a.BrukerId equals b.Id into brukere
                           from b in brukere.DefaultIfEmpty()
                           select new { Abonnement = a, Navn = b == null ? a.BrukerId : b.Navn }).ToListAsync(ct);
        var historikk = await db.Epostadministratorstanser.AsNoTracking().ToListAsync(ct);
        var siste = historikk.GroupBy(x => x.BrukerId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.Id).First());
        return rader.Select(x =>
        {
            var a = x.Abonnement; var utvalg = Abonnementsutvalg.Les(a.UtvalgJson);
            siste.TryGetValue(a.BrukerId, out var stans);
            return new EpostabonnentDto(a.BrukerId, string.IsNullOrWhiteSpace(x.Navn) ? a.BrukerId : x.Navn,
                a.Adresse, a.Status, a.Aktivert.HasValue, a.NyeFrister, a.VesentligeEndringer,
                utvalg.Budsjettaar, utvalg.Kategorier, a.Opprettet, a.Endret, a.Aktivert, a.Versjon,
                stans?.AktoerId, stans?.Tidspunkt);
        }).OrderBy(x => x.Navn, StringComparer.Create(CultureInfo.GetCultureInfo("nb-NO"), true))
            .ThenBy(x => x.BrukerId, StringComparer.Ordinal).ToArray();
    }

    public async Task StansAsync(string brukerId, Guid versjon, bool bekreftet, string aktoerId, CancellationToken ct = default)
    {
        if (!bekreftet) throw new Valideringsfeil("Bekreft at e-postvarslingen skal stanses for denne abonnenten.");
        if (string.IsNullOrWhiteSpace(brukerId) || brukerId.Length > 128 || string.IsNullOrWhiteSpace(aktoerId) || aktoerId.Length > 128)
            throw new Valideringsfeil("Gyldig abonnent og innlogget aktør kreves.");
        await using var db = await fabrikk.CreateDbContextAsync(ct);
        await using var grense = await Epostoverlevering.LaasAsync(db, brukerId, ct);
        // Les først etter rekkefølge- og abonnementslåsen, aldri blind retry over ny påmelding.
        var rad = await db.Epostabonnementer.SingleOrDefaultAsync(x => x.BrukerId == brukerId, ct);
        if (rad is null) throw new Valideringsfeil("Abonnementet finnes ikke. Oppdater oversikten.");
        if (rad.Versjon != versjon) throw new EpostabonnementKonflikt();
        if (rad.Status == "Avmeldt") return;
        var naa = klokke.GetUtcNow();
        EpostabonnementTilstand.Stans(rad, naa);
        db.Epostadministratorstanser.Add(new() { BrukerId = brukerId, AktoerId = aktoerId, Tidspunkt = naa });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new EpostabonnementKonflikt(); }
        await grense.CommitAsync(ct);
    }
}
