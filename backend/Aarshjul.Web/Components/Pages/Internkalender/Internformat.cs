using System.Globalization;

namespace Aarshjul.Web.Components.Pages.Internkalender;

internal static class Internformat
{
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
    public static string Tid(DateTimeOffset? tid) => tid is { } t
        ? TimeZoneInfo.ConvertTime(t, Oslo).ToString("dd.MM.yyyy 'kl.' HH:mm", CultureInfo.GetCultureInfo("nb-NO")) + " (Oslo)"
        : "Ikke synkronisert";
}
