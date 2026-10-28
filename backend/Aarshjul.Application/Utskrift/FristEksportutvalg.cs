using Aarshjul.Application.Frister;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Synlighet;

namespace Aarshjul.Application.Utskrift;

/// <summary>Samme publikum og inklusive datovindu for forhåndsvisning, Word og ICS.</summary>
public sealed class FristEksportutvalg(IFristlesing lesing, IGruppetjeneste grupper)
{
    public async Task<Eksportutvalg> HentAsync(bool alt, string? gruppeKode, DateOnly? fra, DateOnly? til, CancellationToken ct = default)
    {
        ISynlighetskontekst kontekst;
        string etikett;
        if (alt)
        {
            kontekst = new Synlighetskontekst(serAlt: true, grupper: []);
            etikett = "alle";
        }
        else if (!string.IsNullOrWhiteSpace(gruppeKode))
        {
            kontekst = Synlighetskontekst.ForGruppe(gruppeKode);
            var aktive = await grupper.HentAktiveAsync(ct);
            etikett = aktive.FirstOrDefault(g => g.Kode == gruppeKode)?.Navn ?? gruppeKode;
        }
        else throw new Valideringsfeil("Velg en synlighetsgruppe eller «alt».");

        var frister = await lesing.HentSynligeAsync(kontekst,
            new FristFilter { FraDato = fra, TilDato = til, InkluderHistorikk = true }, ct);
        return new(new Utskriftsforesporsel(alt ? null : gruppeKode, etikett, fra, til, alt), frister,
            $"{(alt ? "alle" : gruppeKode!)}-{fra:yyyyMMdd}_{til:yyyyMMdd}");
    }
}

public sealed record Eksportutvalg(Utskriftsforesporsel Foresporsel, IReadOnlyList<FristDto> Frister, string Filnavnsdel);
