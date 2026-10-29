using System.Text.Json;
using Aarshjul.Application;
using Aarshjul.Domain;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Felles vesentlighetsregel for direkte redigering og godkjenning av endringsforslag.</summary>
internal static class Fristendringshendelse
{
    internal static NyFristHendelse? Lag(Frist frist, Fristtilstand foer, bool vesentlig,
        string? forklaring, TimeProvider klokke)
    {
        var etter = Fristtilstand.Fra(frist);
        if (vesentlig && (foer == etter || string.IsNullOrWhiteSpace(forklaring)
            || forklaring.Trim().Length > 300 || forklaring.Any(char.IsControl)))
            throw new Valideringsfeil("En vesentlig innholdsendring krever faktisk endret innhold og en kort forklaring på høyst 300 tegn.");
        if (frist.Status != FristStatus.Godkjent || !(foer.ErAutomatiskVesentlig(etter) || vesentlig)) return null;
        return new NyFristHendelse
        {
            FristId = frist.Id, Type = "Endret", OpprettetUtcTicks = klokke.GetUtcNow().UtcTicks,
            FoerJson = JsonSerializer.Serialize(foer), EtterJson = JsonSerializer.Serialize(etter),
            Forklaring = vesentlig ? forklaring!.Trim() : null
        };
    }
}
