namespace Aarshjul.Domain;

/// <summary>Publiseringshendelse. Navn og ID-er bevares fra EV03.</summary>
public sealed class NyFristHendelse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FristId { get; set; }
    public long OpprettetUtcTicks { get; set; }
    public long Publiseringssekvens { get; set; }
    public string Type { get; set; } = "Ny";
    public string? FoerJson { get; set; }
    public string? EtterJson { get; set; }
    public string? Forklaring { get; set; }
}

/// <summary>Siste publiserte publikum ved eksplisitt sletting; uavhengig av dagens grupperegister.</summary>
public sealed class FristHendelsessynlighet
{
    public Guid HendelseId { get; set; }
    public required string GruppeKode { get; set; }
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
    public int AntallForsoek { get; set; }
    public long? ForsoekStartetUtcTicks { get; set; }
    public long? NesteForsoekUtcTicks { get; set; }
    public long? NesteOppslagUtcTicks { get; set; }
    /// <summary>Bare hash av avmeldingsretten. Råverdien finnes kun i overlevert melding.</summary>
    public string? AvmeldingsHash { get; set; }
    public long? Avmeldingssekvens { get; set; }
    public Guid Versjon { get; set; } = Guid.NewGuid();
    public ICollection<Oppsummeringshendelse> Hendelser { get; set; } = new List<Oppsummeringshendelse>();
}

/// <summary>Felles stans for dagsjobben; endrer aldri kalenderens publiseringsrett.</summary>
public sealed class Epostdriftstilstand
{
    public int Id { get; set; } = 1;
    public bool Stanset { get; set; }
    public string? AktoerId { get; set; }
    public long? EndretUtcTicks { get; set; }
    public Guid Versjon { get; set; }
}

/// <summary>Forsøksmetadata uten mottakeradresse, lenker, meldingsinnhold eller leverandørtekst.</summary>
public sealed class Epostsendeforsoek
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OppsummeringId { get; set; }
    public int Nummer { get; set; }
    public long StartetUtcTicks { get; set; }
    public long? AvsluttetUtcTicks { get; set; }
    public string Status { get; set; } = "UnderSending";
}

/// <summary>Eksplisitt behandlet hendelse: tidsstempler alene kan miste en sen transaksjonscommit.</summary>
public sealed class Oppsummeringshendelse
{
    public Guid OppsummeringId { get; set; }
    public Epostoppsummering Oppsummering { get; set; } = null!;
    public Guid HendelseId { get; set; }
}
