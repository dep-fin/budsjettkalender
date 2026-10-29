namespace Aarshjul.Domain;

/// <summary>Varig revisjonsspor uten adresse, meldingsinnhold eller hemmelige lenker.</summary>
public sealed class Epostadministratorstans
{
    public long Id { get; set; }
    public required string BrukerId { get; set; }
    public required string AktoerId { get; set; }
    public DateTimeOffset Tidspunkt { get; set; }
    public string Handling { get; set; } = "Stanset";
}
