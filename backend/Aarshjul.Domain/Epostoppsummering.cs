namespace Aarshjul.Domain;

/// <summary>Første manuelle publisering. Identiteten bevares også når fristen senere fjernes.</summary>
public sealed class NyFristHendelse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FristId { get; set; }
    public long OpprettetUtcTicks { get; set; }
    public long Publiseringssekvens { get; set; }
}

public sealed class Epostoppsummering
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string BrukerId { get; set; }
    public DateOnly LokalDato { get; set; }
    public long AktivertUtcTicks { get; set; }
    public string Status { get; set; } = "Planlagt";
    public string? Utsettelsesgrunn { get; set; }
    public long? BekreftetUtcTicks { get; set; }
    public Guid Versjon { get; set; } = Guid.NewGuid();
    public ICollection<Oppsummeringshendelse> Hendelser { get; set; } = new List<Oppsummeringshendelse>();
}

/// <summary>Eksplisitt behandlet hendelse: tidsstempler alene kan miste en sen transaksjonscommit.</summary>
public sealed class Oppsummeringshendelse
{
    public Guid OppsummeringId { get; set; }
    public Epostoppsummering Oppsummering { get; set; } = null!;
    public Guid HendelseId { get; set; }
}
