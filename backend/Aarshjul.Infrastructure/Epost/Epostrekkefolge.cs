namespace Aarshjul.Infrastructure.Epost;

internal sealed class Epostrekkefolge
{
    public int Id { get; set; }
    public long Verdi { get; set; }
}

internal sealed class Epostaktivering
{
    public required string BrukerId { get; set; }
    public long Sekvens { get; set; }
    public long Endringssekvens { get; set; }
    public long Livssyklussekvens { get; set; }
}
