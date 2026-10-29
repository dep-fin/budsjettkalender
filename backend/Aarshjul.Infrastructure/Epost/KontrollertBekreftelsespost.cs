using System.Collections.Concurrent;
using Aarshjul.Application.Epost;

namespace Aarshjul.Infrastructure.Epost;

/// <summary>Kun lokal demo/test. Meldinger finnes bare i minnet hos en kontrollert mottaker.</summary>
public sealed class KontrollertBekreftelsespost : IBekreftelsespost
{
    private readonly ConcurrentDictionary<string, Bekreftelsesmelding> _meldinger = new();
    public bool Tilgjengelig => true;
    public Task SendAsync(string brukerId, string adresse, string token, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _meldinger[brukerId] = new(adresse, token);
        return Task.CompletedTask;
    }
    public Bekreftelsesmelding? Hent(string brukerId) => _meldinger.GetValueOrDefault(brukerId);
}

// Klassen har ingen record-ToString som ved et uhell kan skrive ut token i en rapport.
public sealed class Bekreftelsesmelding(string adresse, string token)
{
    public string Adresse { get; } = adresse;
    public string Token { get; } = token;
}

/// <summary>EV02 aktiverer ingen leverandør eller produksjonsutsending.</summary>
public sealed class UtilgjengeligBekreftelsespost : IBekreftelsespost
{
    public bool Tilgjengelig => false;
    public Task SendAsync(string brukerId, string adresse, string token, CancellationToken ct = default)
        => throw new InvalidOperationException("Adressebekreftelse er ikke satt i drift.");
}
