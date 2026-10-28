namespace Aarshjul.Infrastructure.Epost;

public sealed class DagligEpostOpsjoner
{
    public TimeOnly Klokkeslett { get; set; } = new(8, 0);
    public string OffentligBasisadresse { get; set; } = "https://kalender.example/";
}

internal static class Oppsummeringstid
{
    // Manglende sonedata er en konfigurasjonsfeil, aldri en stille UTC-standard.
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
    public static DateTimeOffset Sendetid(DateOnly dato, TimeOnly tid)
    {
        var lokal = dato.ToDateTime(tid, DateTimeKind.Unspecified);
        // Vårgap: første gyldige minutt. Høstoverlapp: første forekomst.
        while (Oslo.IsInvalidTime(lokal)) lokal = lokal.AddMinutes(1);
        var offset = Oslo.IsAmbiguousTime(lokal) ? Oslo.GetAmbiguousTimeOffsets(lokal).Max() : Oslo.GetUtcOffset(lokal);
        return new(lokal, offset);
    }
}
