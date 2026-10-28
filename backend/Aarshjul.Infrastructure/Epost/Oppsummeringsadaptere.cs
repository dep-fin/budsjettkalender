using System.Collections.Concurrent;
using Aarshjul.Application.Synlighet;

namespace Aarshjul.Infrastructure.Epost;

public enum MottakertilgangStatus { Aktiv, Inaktiv, Uavklart }

/// <summary>Et nytt autoritativt oppslag per kall. Ingen fallback til innloggingsclaims.</summary>
public interface IMottakertilgangskilde
{
    Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default);
}

public sealed record Mottakertilgang(MottakertilgangStatus Status, ISynlighetskontekst? Synlighet = null);

/// <summary>En bekreftelse betyr at transporten tok imot meldingen, ikke observert ekstern levering.</summary>
public interface IOppsummeringspost
{
    bool Tilgjengelig { get; }
    Task SendAsync(Oppsummeringsmelding melding, CancellationToken ct = default);
}

// Ingen automatisk ToString med adresse eller fristinnhold.
public sealed class Oppsummeringsmelding(Guid id, string brukerId, string adresse, string tekst, string html)
{
    public Guid Id { get; } = id;
    public string BrukerId { get; } = brukerId;
    public string Adresse { get; } = adresse;
    public string Emne => "Nye frister i budsjettkalenderen";
    public string Tekst { get; } = tekst;
    public string Html { get; } = html;
}

public sealed class UtilgjengeligMottakertilgang : IMottakertilgangskilde
{
    public Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default)
        => Task.FromResult(new Mottakertilgang(MottakertilgangStatus.Uavklart));
}

public sealed class UtilgjengeligOppsummeringspost : IOppsummeringspost
{
    public bool Tilgjengelig => false;
    public Task SendAsync(Oppsummeringsmelding melding, CancellationToken ct = default)
        => throw new InvalidOperationException("Oppsummeringstransport er ikke satt i drift.");
}

/// <summary>Kun demo/Testing. Et selvstendig, kontrollert identitetsregister, aldri appens brukerrader.</summary>
public sealed class KontrollertMottakertilgang : IMottakertilgangskilde
{
    private readonly ConcurrentDictionary<string, Mottakertilgang> _tilganger = new();
    public void Sett(string brukerId, Mottakertilgang tilgang) => _tilganger[brukerId] = tilgang;
    public Task<Mottakertilgang> HentAsync(string brukerId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_tilganger.GetValueOrDefault(brukerId) ?? new(MottakertilgangStatus.Uavklart));
    }
}

public sealed class KontrollertOppsummeringspost : IOppsummeringspost
{
    private readonly ConcurrentQueue<Oppsummeringsmelding> _meldinger = new();
    public bool Stanset { get; set; }
    public bool Tilgjengelig => !Stanset;
    public IReadOnlyList<Oppsummeringsmelding> Hent(string brukerId) => _meldinger.Where(x => x.BrukerId == brukerId).ToList();
    public Task SendAsync(Oppsummeringsmelding melding, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (Stanset) throw new InvalidOperationException("Kontrollert transport er stanset.");
        _meldinger.Enqueue(melding);
        return Task.CompletedTask;
    }
}
