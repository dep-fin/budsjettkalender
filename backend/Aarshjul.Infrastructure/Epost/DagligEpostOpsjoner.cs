namespace Aarshjul.Infrastructure.Epost;

public sealed class DagligEpostOpsjoner
{
    public TimeOnly Klokkeslett { get; set; } = new(8, 0);
    public string OffentligBasisadresse { get; set; } = "https://kalender.example/";
    public int MaksSendeforsoek { get; set; } = 3;
    public TimeSpan Retrypause { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan Oppslagspause { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan Tilgangsfrist { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan Kvitteringsfrist { get; set; } = TimeSpan.FromSeconds(30);
    public int ForsoeksdetaljerDager { get; set; } = 30;
    public int HistoriskInnholdDager { get; set; } = 90;
    public int Ryddebatch { get; set; } = 100;
    public bool Gyldig() => MaksSendeforsoek is >= 1 and <= 10 && Retrypause >= TimeSpan.FromMinutes(5)
        && Retrypause <= TimeSpan.FromDays(7) && Oppslagspause >= TimeSpan.Zero && Oppslagspause <= TimeSpan.FromDays(1)
        && Tilgangsfrist >= TimeSpan.FromSeconds(1) && Tilgangsfrist <= TimeSpan.FromMinutes(1)
        && Kvitteringsfrist >= TimeSpan.FromSeconds(1) && Kvitteringsfrist <= TimeSpan.FromMinutes(5)
        && ForsoeksdetaljerDager is >= 1 and <= 3650 && HistoriskInnholdDager is >= 1 and <= 3650 && Ryddebatch is >= 1 and <= 1000;
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
