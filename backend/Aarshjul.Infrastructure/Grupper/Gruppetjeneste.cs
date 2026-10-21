using Aarshjul.Application;
using Aarshjul.Application.Grupper;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Grupper;

/// <summary>Leser synlighetsgrupper fra databasen for synlighetsvalg og administrasjon.</summary>
public class Gruppetjeneste(IDbContextFactory<AppDbContext> dbFactory) : IGruppetjeneste
{
    public async Task<IReadOnlyList<GruppeadministrasjonDto>> HentAlleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Synlighetsgrupper.AsNoTracking()
            .OrderByDescending(g => g.ErStandard).ThenBy(g => g.Navn)
            .Select(g => new GruppeadministrasjonDto(g.Kode, g.Navn, g.Aktiv, g.ErStandard))
            .ToListAsync(ct);
    }

    public async Task OpprettAsync(string kode, string navn, CancellationToken ct = default)
    {
        kode = ValiderKode(kode);
        navn = ValiderNavn(navn);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await KodeFinnesAsync(db, kode, ct))
            throw new Valideringsfeil("Gruppekoden finnes allerede, også om gruppen er inaktiv.");

        db.Synlighetsgrupper.Add(new Synlighetsgruppe
        {
            Id = Guid.NewGuid(), Kode = kode, Navn = navn
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Den unike databasenøkkelen avgjør også når to administratorer oppretter samtidig.
            if (await KodeFinnesAsync(db, kode, ct))
                throw new Valideringsfeil("Gruppekoden finnes allerede, også om gruppen er inaktiv.");
            throw;
        }
    }

    public async Task OppdaterAsync(string kode, string navn, bool aktiv, CancellationToken ct = default)
    {
        navn = ValiderNavn(navn);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var gruppe = await db.Synlighetsgrupper.SingleOrDefaultAsync(g => g.Kode == kode, ct)
            ?? throw new Valideringsfeil("Gruppen finnes ikke.");
        gruppe.Navn = navn;
        gruppe.Aktiv = aktiv;
        await db.SaveChangesAsync(ct);
    }

    private static Task<bool> KodeFinnesAsync(AppDbContext db, string kode, CancellationToken ct)
        // Samme sammenligning på SQLite og databaser med ulik kollasjon; også eldre koder reserveres.
        => db.Synlighetsgrupper.AnyAsync(g => g.Kode.ToUpper() == kode, ct);

    private static string ValiderKode(string kode)
    {
        var trimmet = kode?.Trim() ?? "";
        if (trimmet.Length is < 1 or > 64 || !char.IsAsciiLetter(trimmet[0])
            || trimmet.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new Valideringsfeil("Kode må ha 1–64 tegn, begynne med en bokstav og bare inneholde A–Z, 0–9 og bindestrek.");
        return trimmet.ToUpperInvariant();
    }

    private static string ValiderNavn(string navn)
    {
        var trimmet = navn?.Trim() ?? "";
        if (trimmet.Length is < 1 or > 256)
            throw new Valideringsfeil("Visningsnavn må ha 1–256 tegn.");
        return trimmet;
    }

    public async Task<IReadOnlyList<GruppeDto>> HentAktiveAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Synlighetsgrupper
            .Where(g => g.Aktiv)
            .OrderByDescending(g => g.ErStandard)
            .ThenBy(g => g.Navn)
            .Select(g => new GruppeDto(g.Kode, g.Navn, g.ErStandard))
            .ToListAsync(ct);
    }
}
