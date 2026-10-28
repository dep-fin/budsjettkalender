namespace Aarshjul.Domain;

/// <summary>Selvvalgt e-postvarsling. Identiteten kommer fra innlogging, aldri adressen.</summary>
public sealed class Epostabonnement
{
    public required string BrukerId { get; set; }
    public required string Adresse { get; set; }
    public string Status { get; set; } = "VenterPaaBekreftelse";
    public bool NyeFrister { get; set; } = true;
    public bool VesentligeEndringer { get; set; }
    public bool AlleBudsjettaar { get; set; } = true;
    public bool AlleKategorier { get; set; } = true;
    public DateTimeOffset Opprettet { get; set; }
    public DateTimeOffset Endret { get; set; }
    public DateTimeOffset? Aktivert { get; set; }
    public string? BekreftelsesHash { get; set; }
    public DateTimeOffset? BekreftelseUtloeper { get; set; }
    public int Bekreftelsesforsoek { get; set; }
    public DateTimeOffset SistSendt { get; set; }
    public DateTimeOffset SendevinduStart { get; set; }
    public int AntallSendt { get; set; }
    public Guid Versjon { get; set; } = Guid.NewGuid();
}
