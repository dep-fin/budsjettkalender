using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Frister;

/// <summary>
/// Databasenær implementasjon av <see cref="IFristlesing"/>. Synlighetsfiltreringen påføres
/// spørringen før projeksjon, slik at frister brukeren ikke har rett til aldri hentes fra
/// databasen — og dermed aldri kan sendes til klienten.
/// </summary>
public class FristTjeneste(IDbContextFactory<AppDbContext> dbFactory, TimeProvider klokke) : IFristlesing
{
    public async Task<IReadOnlyList<FristDto>> HentSynligeAsync(
        ISynlighetskontekst ctx, FristFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var idag = Idag();
        var spørring = GrunnSpørring(db, ctx, filter, idag);
        return await ProjiserAsync(db, spørring.OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Tittel), ctx.SerAlt, ct);
    }

    public async Task<IReadOnlyList<FristDto>> HentLandingsutvalgAsync(
        ISynlighetskontekst ctx, DateOnly fraOgMed, FristFilter filter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Landingsflaten er alltid framoverskuende fra angitt dag.
        var spørring = GrunnSpørring(db, ctx, filter, fraOgMed, kunFramover: true)
            .OrderBy(f => f.Sorteringsdag).ThenBy(f => f.Tittel);

        var kommende = await ProjiserAsync(db, spørring, ctx.SerAlt, ct);

        // Union av «innen 30 dager» og «minst de fem førstkommende».
        var grense = fraOgMed.AddDays(30);
        return kommende
            .Where((f, indeks) => indeks < 5 || f.Sorteringsdag <= grense)
            .ToList();
    }

    public async Task<FristDto?> HentEnAsync(ISynlighetskontekst ctx, Guid fristId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Samme synlighetsfilter som listene — en frist konteksten ikke ser hentes aldri fra db.
        var spørring = db.Frister
            .AsNoTracking()
            .Where(f => f.Id == fristId && f.Status == FristStatus.Godkjent)
            .FiltrerSynlige(ctx);

        return await Projiser(db, spørring, ctx.SerAlt).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<int>> HentTilgjengeligeBudsjettaarAsync(
        ISynlighetskontekst ctx, bool inkluderHistorikk, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var idag = Idag();
        var spørring = db.Frister
            .AsNoTracking()
            .Where(f => f.Status == FristStatus.Godkjent)
            .FiltrerSynlige(ctx);

        if (!inkluderHistorikk)
        {
            spørring = spørring.Where(f => f.Sorteringsdag >= idag);
        }

        return await spørring
            .Select(f => f.Budsjettaar)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    /// <summary>Synlige, godkjente frister som matcher kategori-/budsjettårsfilter og historikkregel.</summary>
    private static IQueryable<Frist> GrunnSpørring(AppDbContext db, ISynlighetskontekst ctx, FristFilter filter, DateOnly idag, bool kunFramover = false)
    {
        var spørring = db.Frister
            .AsNoTracking()
            .Where(f => f.Status == FristStatus.Godkjent)
            .FiltrerSynlige(ctx);

        if (filter.Kategorier is { Count: > 0 } kategorier)
        {
            spørring = spørring.Where(f => kategorier.Contains(f.Kategori));
        }

        if (filter.Budsjettaar is { Count: > 0 } aar)
        {
            spørring = spørring.Where(f => aar.Contains(f.Budsjettaar));
        }

        // Periodevindu (utskrift): begrens på sorteringsdag. Settes uavhengig av historikkregelen.
        if (filter.FraDato is { } fra)
        {
            spørring = spørring.Where(f => f.Sorteringsdag >= fra);
        }

        if (filter.TilDato is { } til)
        {
            spørring = spørring.Where(f => f.Sorteringsdag <= til);
        }

        // Frist vises t.o.m. fristdagen; flyttes til historikk dato + 1 (ren kalender, ingen forsinket-tilstand).
        if (kunFramover || !filter.InkluderHistorikk)
        {
            spørring = spørring.Where(f => f.Sorteringsdag >= idag);
        }

        return spørring;
    }

    private static Task<List<FristDto>> ProjiserAsync(AppDbContext db, IQueryable<Frist> spørring, bool serAlt, CancellationToken ct)
        => Projiser(db, spørring, serAlt).ToListAsync(ct);

    private static IQueryable<FristDto> Projiser(AppDbContext db, IQueryable<Frist> spørring, bool serAlt)
        => spørring
            .Select(f => new FristDto(
                f.Id,
                f.Tittel,
                f.Dato,
                f.Datopresisjon,
                f.Datokvalifikator,
                f.Sorteringsdag,
                f.Budsjettaar,
                f.Kategori,
                f.Loep,
                TillattKildeetikett(f.Kilde, serAlt),
                f.DokumentId,
                f.Notat,
                f.Status,
                f.Synlighet.Select(s => s.GruppeKode).ToList(),
                f.Klokkeslett,
                TillattKildeUrl(db.BehandledeDokumenter.Where(d => d.Id == f.DokumentId).Select(d => d.Url).FirstOrDefault()),
                serAlt && db.Forslag.Any(forslag => forslag.Status == FristStatus.Forslag
                    && forslag.ForslagType == ForslagType.Endring && forslag.EndrerFristId == f.Id)));

    // Opplastingsetiketten bærer administratorens lokale filnavn. Fullt innsyn beholder
    // den; gruppelesing (også «se som gruppe») får fortsatt øvrige etablerte kilder.
    private static string? TillattKildeetikett(string? kilde, bool serAlt)
        => !serAlt && kilde?.StartsWith("Opplastet: ", StringComparison.Ordinal) == true ? null : kilde;

    // Kalles i den siste projeksjonen, etter SQL-filteret for fristens synlighet.
    // Bare dokumentets lagrede adresse brukes; ingen gjetting fra kilde, filnavn eller opphav.
    private static string? TillattKildeUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.IsWellFormedOriginalString()
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Port != 443
            || uri.GetComponents(UriComponents.UserInfo | UriComponents.KeepDelimiter, UriFormat.UriEscaped).Length != 0
            || !(uri.Host.Equals("regjeringen.no", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.regjeringen.no", StringComparison.OrdinalIgnoreCase)))
            return null;

        return uri.AbsoluteUri;
    }

    private DateOnly Idag() => Datohjelp.Idag(klokke);
}
