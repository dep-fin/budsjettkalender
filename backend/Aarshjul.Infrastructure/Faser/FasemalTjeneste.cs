using System.Text.Json;
using Aarshjul.Application.Faser;
using Microsoft.EntityFrameworkCore;

namespace Aarshjul.Infrastructure.Faser;

public sealed class FasemalTjeneste(IDbContextFactory<AppDbContext> dbFactory) : IFasemal
{
    public async Task<FasemalDto> HentAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rad = await db.Fasemaler.AsNoTracking().SingleAsync(ct);
        return TilDto(rad);
    }

    public async Task<FasemalDto> OppdaterAsync(Guid forventetVersjon, IReadOnlyList<FaseDto> faser,
        int sisteMaanedInkludert, CancellationToken ct = default)
    {
        var innhold = new Fasemalinnhold(Fasemalvalidering.Valider(faser, sisteMaanedInkludert), sisteMaanedInkludert);
        var json = JsonSerializer.Serialize(innhold);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rad = await db.Fasemaler.SingleAsync(ct);
        if (rad.Versjon != forventetVersjon)
            throw new FasemalKonfliktunntak();

        // Sammenlign også i UPDATE: en annen operasjon kan ha lagret etter lesingen over.
        db.Entry(rad).Property(x => x.Versjon).OriginalValue = forventetVersjon;
        rad.Innhold = json;
        rad.Versjon = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex) when (
            ex.Entries.Count > 0 && ex.Entries.All(e => e.Entity is FasemalRad))
        {
            throw new FasemalKonfliktunntak();
        }

        return new(rad.Versjon, innhold.Faser, innhold.SisteMaanedInkludert);
    }

    private static FasemalDto TilDto(FasemalRad rad)
    {
        var innhold = JsonSerializer.Deserialize<Fasemalinnhold>(rad.Innhold)
            ?? throw new InvalidOperationException("Den lagrede fasemalen mangler innhold.");
        return new(rad.Versjon, Array.AsReadOnly(innhold.Faser.ToArray()), innhold.SisteMaanedInkludert);
    }
}
