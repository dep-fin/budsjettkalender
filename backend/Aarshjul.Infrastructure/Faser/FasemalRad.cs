using System.Text.Json;
using Aarshjul.Application.Faser;

namespace Aarshjul.Infrastructure.Faser;

/// <summary>Lagringsformat for den ene fasemalen, uten kobling til frister eller regler.</summary>
internal sealed class FasemalRad
{
    public int Id { get; set; }
    public string Innhold { get; set; } = "";
    public Guid Versjon { get; set; }
}

internal sealed record Fasemalinnhold(IReadOnlyList<FaseDto> Faser, int SisteMaanedInkludert)
{
    public static string StandardJson { get; } = JsonSerializer.Serialize(new Fasemalinnhold(
        Fasemalstandard.Standard.Faser, Fasemalstandard.Standard.SisteMaanedInkludert));
}
