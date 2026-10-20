using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Generering;
using Aarshjul.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Datautveksling;

/// <summary>
/// Eksporterer og importerer alle gjentaksregler (årsmalen) som en JSON-«database» — tilsvarende
/// <see cref="FristDatautveksling"/> for frister. Import erstatter alt: eksisterende regler fjernes
/// og fila blir fasiten. Regler med parametre som ikke lar seg tolke for regeltypen, hoppes over med
/// en advarsel, slik at en senere genereringskjøring ikke feiler på en ugyldig regel.
/// </summary>
public sealed class MalDatautveksling(IDbContextFactory<AppDbContext> dbFactory, TimeProvider klokke) : IMalDatautveksling
{
    public async Task<MalDatabase> EksporterAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var regler = await db.Gjentaksregler
            .AsNoTracking()
            .OrderBy(r => r.Loep)
            .ToListAsync(ct);

        return new MalDatabase
        {
            Versjon = 1,
            EksportertTid = klokke.GetUtcNow(),
            Regler = regler.Select(r => new MalRegelEksport
            {
                Id = r.Id,
                Loep = r.Loep,
                Tittel = r.Tittel,
                Kategori = r.Kategori,
                Regeltype = r.Regeltype,
                Regelparametre = r.Regelparametre,
                Valgaarssensitiv = r.Valgaarssensitiv
            }).ToList()
        };
    }

    public async Task<ImportResultat> ImporterAsync(MalDatabase database, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        ArgumentNullException.ThrowIfNull(database);
        Importvalidering.Kontroller(database.Versjon, database.Regler, r => r.Id);

        var advarsler = new List<string>();
        var kandidater = new List<Gjentaksregel>();
        foreach (var kilde in database.Regler)
        {
            if (string.IsNullOrWhiteSpace(kilde.Loep))
            {
                advarsler.Add($"Hoppet over en regel uten løp (tittel «{kilde.Tittel}»).");
                continue;
            }

            try
            {
                Regelparser.Valider(kilde.Regeltype, kilde.Regelparametre);
            }
            catch (FormatException e)
            {
                advarsler.Add($"«{(string.IsNullOrWhiteSpace(kilde.Tittel) ? kilde.Loep : kilde.Tittel)}»: hoppet over — {e.Message}");
                continue;
            }

            kandidater.Add(new Gjentaksregel
            {
                Id = kilde.Id == Guid.Empty ? Guid.NewGuid() : kilde.Id,
                Loep = kilde.Loep.Trim(),
                Tittel = (kilde.Tittel ?? "").Trim(),
                Kategori = kilde.Kategori,
                Regeltype = kilde.Regeltype,
                Regelparametre = kilde.Regelparametre.Trim(),
                Valgaarssensitiv = kilde.Valgaarssensitiv
            });
        }

        var eksisterende = await db.Gjentaksregler.ToListAsync(ct);
        db.Gjentaksregler.RemoveRange(eksisterende);
        db.Gjentaksregler.AddRange(kandidater);
        await db.SaveChangesAsync(ct);
        return new ImportResultat(kandidater.Count, eksisterende.Count, advarsler);
    }
}
