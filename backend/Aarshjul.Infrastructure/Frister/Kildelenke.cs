using Aarshjul.Application;

namespace Aarshjul.Infrastructure.Frister;

/// <summary>Felles serverregel for manuelle og dokumentbaserte offentlige kildelenker.</summary>
internal static class Kildelenke
{
    internal static string? ForLagring(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return TillattUrl(url) ?? throw new Valideringsfeil(
            "Kildelenke må være en absolutt HTTPS-adresse på regjeringen.no eller www.regjeringen.no, uten brukerinformasjon og med port 443.", "ManuellKildeUrl");
    }

    internal static string? TillattUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !uri.IsWellFormedOriginalString()
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Port != 443
            || uri.GetComponents(UriComponents.UserInfo | UriComponents.KeepDelimiter, UriFormat.UriEscaped).Length != 0
            || !(uri.Host.Equals("regjeringen.no", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.regjeringen.no", StringComparison.OrdinalIgnoreCase)))
            return null;
        return uri.AbsoluteUri;
    }
}
