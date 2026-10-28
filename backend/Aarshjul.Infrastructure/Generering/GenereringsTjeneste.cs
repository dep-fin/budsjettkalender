using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Generering;

/// <summary>
/// Genererer et budsjettår fra årsmalen og legger forslagene på generér-flaten (adskilt fra køen).
/// Beregningen er ren (<see cref="Genereringsberegning"/>); denne tjenesten henter regler,
/// viderefører synlighet fra fjorårets godkjente frist (ellers synlighetsregelen), og lagrer
/// <see cref="ForslagType.Generert"/>-forslag. Ingenting publiseres her — godkjenning skjer via
/// godkjenningskøens publiseringsmønster.
/// </summary>
public class GenereringsTjeneste(IDbContextFactory<AppDbContext> dbFactory, ISynlighetsregel synlighetsregel) : IGenereringstjeneste
{
    public async Task<MalForhaandsvisning> ProevAsync(GenereringsForespoersel forespoersel, CancellationToken ct = default)
    {
        if (forespoersel.Maalbudsjettaar is < 1 or > 9999)
            throw new Valideringsfeil("Budsjettåret må være mellom 1 og 9999.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var beregnet = await BeregnAsync(db, forespoersel, ct);
        return new MalForhaandsvisning(forespoersel.Maalbudsjettaar,
            beregnet.Select(r => new MalRegelDto(r.Regel.Id, r.Regel.Loep, r.Regel.Tittel,
                r.Regel.Kategori, r.Regel.Regeltype, r.Regel.Regelparametre, r.Regel.Valgaarssensitiv)).ToList(),
            beregnet.Select(r => new MalRegelForhaandsvisning(forespoersel.Maalbudsjettaar,
                r.Dato, r.Presisjon, r.Tentativ, r.Valgaarsflagg, r.Feil)).ToList());
    }

    public async Task<GenereringsResultat> GenererAsync(GenereringsForespoersel forespoersel, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var maalaar = forespoersel.Maalbudsjettaar;
        var beregnet = await BeregnAsync(db, forespoersel, ct);

        // Fjorårets godkjente frister per løp, for videreføring av synlighet.
        var fjoraar = await db.Frister
            .AsNoTracking()
            .Include(f => f.Synlighet)
            .Where(f => f.Budsjettaar == maalaar - 1 && f.Status == FristStatus.Godkjent && f.Loep != null)
            .ToListAsync(ct);
        var fjoraarPerLoep = fjoraar
            .GroupBy(f => f.Loep!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Erstatt tidligere åpne genererte forslag for året (idempotent re-generering).
        var gamle = await db.Forslag
            .Where(f => f.ForslagType == ForslagType.Generert
                        && f.Status == FristStatus.Forslag
                        && f.Budsjettaar == maalaar)
            .ToListAsync(ct);
        db.Forslag.RemoveRange(gamle);

        var feil = new List<string>();
        var ankreSomMaaSettes = new List<string>();
        var antall = 0;

        foreach (var r in beregnet)
        {
            if (r.ErFeil)
            {
                feil.Add($"{Visningsnavn(r.Regel)}: {r.Feil}");
                // En uoppløselig kjede skyldes ofte et valgårssensitivt anker som må settes manuelt.
                if (r.Regel.Valgaarssensitiv)
                {
                    ankreSomMaaSettes.Add(r.Regel.Loep);
                }
                continue;
            }

            db.Forslag.Add(new Forslag
            {
                Id = Guid.NewGuid(),
                ForslagType = ForslagType.Generert,
                Opphav = Opphav.Admin,
                KildeEllerInnsender = $"Generert {maalaar}",
                Tittel = Visningsnavn(r.Regel),
                Dato = r.Dato,
                Datopresisjon = r.Presisjon,
                Datokvalifikator = null,
                Budsjettaar = maalaar,
                Kategori = r.Regel.Kategori,
                Loep = r.Regel.Loep,
                Status = FristStatus.Forslag,
                ForeslaattSynlighet = JsonSerializer.Serialize(
                    VidérefoertSynlighet(r.Regel.Loep, fjoraarPerLoep))
            });
            antall++;
        }

        await db.SaveChangesAsync(ct);

        return new GenereringsResultat(maalaar, antall, feil, ankreSomMaaSettes.Distinct().ToList());
    }

    public async Task<IReadOnlyList<GenerertForslagDto>> HentGenererteAsync(int maalbudsjettaar, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var forslag = await db.Forslag
            .AsNoTracking()
            .Where(f => f.ForslagType == ForslagType.Generert
                        && f.Status == FristStatus.Forslag
                        && f.Budsjettaar == maalbudsjettaar)
            .ToListAsync(ct);

        // Fjorårets datoer for sammenligning, og reglene for å avlede valgårsmarkering.
        var fjoraar = await db.Frister
            .AsNoTracking()
            .Where(f => f.Budsjettaar == maalbudsjettaar - 1 && f.Status == FristStatus.Godkjent && f.Loep != null)
            .ToListAsync(ct);
        var fjoraarPerLoep = fjoraar
            .GroupBy(f => f.Loep!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var valgaarssensitive = await db.Gjentaksregler
            .AsNoTracking()
            .Where(r => r.Valgaarssensitiv)
            .Select(r => r.Loep)
            .ToListAsync(ct);
        var valgaarssensitivSet = valgaarssensitive.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return forslag
            .Select(f =>
            {
                var fjor = f.Loep is not null && fjoraarPerLoep.TryGetValue(f.Loep, out var match) ? match : null;
                return new GenerertForslagDto
                {
                    ForslagId = f.Id,
                    Tittel = f.Tittel,
                    Loep = f.Loep,
                    Kategori = f.Kategori,
                    Dato = f.Dato,
                    Datopresisjon = f.Datopresisjon,
                    Datokvalifikator = f.Datokvalifikator,
                    Tentativ = f.Datopresisjon != Datopresisjon.Dag,
                    Valgaarsflagg = f.Loep is not null
                                    && valgaarssensitivSet.Contains(f.Loep)
                                    && f.Dato is { } d
                                    && Valgaar.Type(d.Year) != Valgtype.Ingen,
                    ForeslaattSynlighet = LesSynlighet(f.ForeslaattSynlighet),
                    FjoraarDato = fjor?.Dato,
                    FjoraarDatopresisjon = fjor?.Datopresisjon,
                    FjoraarDatokvalifikator = fjor?.Datokvalifikator
                };
            })
            .OrderBy(d => d.Dato ?? DateOnly.MaxValue)
            .ThenBy(d => d.Tittel)
            .ToList();
    }

    public async Task<IReadOnlyList<int>> HentAarMedForslagAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Forslag
            .AsNoTracking()
            .Where(f => f.ForslagType == ForslagType.Generert && f.Status == FristStatus.Forslag)
            .Select(f => f.Budsjettaar)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Videreført synlighet: fjorårets godkjente frist for samme løp gir standard. POL bæres med
    /// som forslag (vises for administrator), men krever aktiv bekreftelse ved godkjenning — det
    /// settes aldri stilltiende. Uten fjorårsfrist brukes synlighetsregelens default (uten POL).
    /// </summary>
    private List<string> VidérefoertSynlighet(string loep, IReadOnlyDictionary<string, Frist> fjoraarPerLoep)
    {
        if (fjoraarPerLoep.TryGetValue(loep, out var fjor))
        {
            return fjor.Synlighet.Select(s => s.GruppeKode).ToList();
        }

        return synlighetsregel.StandardForslagssynlighet().ToList();
    }

    private static string Visningsnavn(Gjentaksregel regel)
        => string.IsNullOrWhiteSpace(regel.Tittel) ? regel.Loep : regel.Tittel;

    private static async Task<IReadOnlyList<BeregnetRegel>> BeregnAsync(
        AppDbContext db, GenereringsForespoersel forespoersel, CancellationToken ct)
    {
        // Første regel per løp er ankerrepresentant. Visningssortering må aldri endre dette.
        var regler = await db.Gjentaksregler.AsNoTracking().ToListAsync(ct);
        ct.ThrowIfCancellationRequested();
        return Genereringsberegning.Beregn(forespoersel.Maalbudsjettaar, regler, forespoersel.ManuelleAnkre);
    }

    private static IReadOnlyList<string> LesSynlighet(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
