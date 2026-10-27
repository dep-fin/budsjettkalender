using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Generering;

/// <summary>
/// Forvalter gjentaksreglene i årsmalen. Validerer at <see cref="Gjentaksregel.Regelparametre"/>
/// kan tolkes for den valgte regeltypen (via <see cref="Regelparser"/>) før lagring, slik at en
/// senere genereringskjøring ikke feiler på en ugyldig regel.
/// </summary>
public class Maltjeneste(IDbContextFactory<AppDbContext> dbFactory) : IMaltjeneste
{
    public async Task<MalForhaandsvisning> ForhaandsvisAlleAsync(int budsjettaar, CancellationToken ct = default)
    {
        if (budsjettaar is < 1 or > 9999)
            throw new Valideringsfeil("Budsjettåret må være mellom 1 og 9999.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Behold genereringens usorterte innlasting: første regel per løp er anker.
        var regler = await db.Gjentaksregler.AsNoTracking().ToListAsync(ct);
        ct.ThrowIfCancellationRequested();
        var beregninger = Genereringsberegning.Beregn(budsjettaar, regler);
        return new MalForhaandsvisning(budsjettaar,
            regler.Select(r => new MalRegelDto(r.Id, r.Loep, r.Tittel, r.Kategori,
                r.Regeltype, r.Regelparametre, r.Valgaarssensitiv)).ToList(),
            beregninger.Select(r => new MalRegelForhaandsvisning(budsjettaar,
                r.Dato, r.Presisjon, r.Tentativ, r.Valgaarsflagg, r.Feil)).ToList());
    }

    public async Task<IReadOnlyList<MalRegelDto>> HentAlleAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Gjentaksregler
            .AsNoTracking()
            .OrderBy(r => r.Loep)
            .Select(r => new MalRegelDto(r.Id, r.Loep, r.Tittel, r.Kategori, r.Regeltype, r.Regelparametre, r.Valgaarssensitiv))
            .ToListAsync(ct);
    }

    public async Task<MalRegelInndata?> HentForRedigeringAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var r = await db.Gjentaksregler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null)
        {
            return null;
        }

        return new MalRegelInndata
        {
            Loep = r.Loep,
            Tittel = r.Tittel,
            Kategori = r.Kategori,
            Regeltype = r.Regeltype,
            Regelparametre = r.Regelparametre,
            Valgaarssensitiv = r.Valgaarssensitiv
        };
    }

    public async Task<IReadOnlyList<MalRegelForhaandsvisning>> ForhaandsvisAsync(
        MalRegelInndata inndata, int foersteBudsjettaar, Guid? id = null, CancellationToken ct = default)
    {
        Valider(inndata);
        if (foersteBudsjettaar is < 1 or > 9995)
        {
            throw new Valideringsfeil("Første budsjettår må være mellom 1 og 9995 for femårsvisningen.");
        }

        // Kopier utkastet før første await, slik at senere feltendringer ikke endrer dette kallet.
        var kandidat = new Gjentaksregel
        {
            Id = id ?? Guid.NewGuid(),
            Loep = inndata.Loep.Trim(),
            Tittel = inndata.Tittel.Trim(),
            Kategori = inndata.Kategori,
            Regeltype = inndata.Regeltype,
            Regelparametre = inndata.Regelparametre.Trim(),
            Valgaarssensitiv = inndata.Valgaarssensitiv
        };

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Samme usorterte innlasting som GenereringsTjeneste: første regel per løp er anker.
        // HentAlleAsyncs visningssortering må ikke velge en annen representant her.
        var regler = await db.Gjentaksregler.AsNoTracking().ToListAsync(ct);
        int kandidatindeks;
        if (id is { } regelId)
        {
            kandidatindeks = regler.FindIndex(r => r.Id == regelId);
            if (kandidatindeks < 0)
            {
                throw new Valideringsfeil("Regelen finnes ikke.");
            }
            regler[kandidatindeks] = kandidat;
        }
        else
        {
            kandidatindeks = regler.Count;
            regler.Add(kandidat);
        }

        var resultater = new List<MalRegelForhaandsvisning>(5);
        for (var forskyvning = 0; forskyvning < 5; forskyvning++)
        {
            ct.ThrowIfCancellationRequested();
            var budsjettaar = foersteBudsjettaar + forskyvning;
            var beregnet = Genereringsberegning.Beregn(budsjettaar, regler)[kandidatindeks];
            resultater.Add(new MalRegelForhaandsvisning(
                budsjettaar, beregnet.Dato, beregnet.Presisjon,
                beregnet.Tentativ, beregnet.Valgaarsflagg, beregnet.Feil));
        }
        return resultater;
    }

    public async Task<Guid> OpprettAsync(MalRegelInndata inndata, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        Valider(inndata);

        var regel = new Gjentaksregel
        {
            Id = Guid.NewGuid(),
            Loep = inndata.Loep.Trim(),
            Tittel = inndata.Tittel.Trim(),
            Kategori = inndata.Kategori,
            Regeltype = inndata.Regeltype,
            Regelparametre = inndata.Regelparametre.Trim(),
            Valgaarssensitiv = inndata.Valgaarssensitiv
        };
        db.Gjentaksregler.Add(regel);
        await db.SaveChangesAsync(ct);
        return regel.Id;
    }

    public async Task OppdaterAsync(Guid id, MalRegelInndata inndata, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        Valider(inndata);

        var regel = await db.Gjentaksregler.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new Valideringsfeil("Regelen finnes ikke.");

        regel.Loep = inndata.Loep.Trim();
        regel.Tittel = inndata.Tittel.Trim();
        regel.Kategori = inndata.Kategori;
        regel.Regeltype = inndata.Regeltype;
        regel.Regelparametre = inndata.Regelparametre.Trim();
        regel.Valgaarssensitiv = inndata.Valgaarssensitiv;

        await db.SaveChangesAsync(ct);
    }

    public async Task SlettAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var regel = await db.Gjentaksregler.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (regel is not null)
        {
            db.Gjentaksregler.Remove(regel);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Validerer pliktfelter og at parametrene kan tolkes for regeltypen.</summary>
    private static void Valider(MalRegelInndata inndata)
    {
        if (string.IsNullOrWhiteSpace(inndata.Loep))
        {
            throw new Valideringsfeil("Løp må fylles ut.");
        }
        if (string.IsNullOrWhiteSpace(inndata.Tittel))
        {
            throw new Valideringsfeil("Tittel må fylles ut.");
        }

        try
        {
            Regelparser.Valider(inndata.Regeltype, inndata.Regelparametre);
        }
        catch (FormatException e)
        {
            throw new Valideringsfeil($"Regelparametrene kan ikke tolkes: {e.Message}");
        }
    }
}
