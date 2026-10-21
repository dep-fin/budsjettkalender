using Azure.Core;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Aarshjul.Web.Konfigurasjon;

internal static class KeyVaultKonfigurasjon
{
    public static void Last(WebApplicationBuilder builder, TokenCredential? legitimasjon, SecretClientOptions? klientopsjoner)
    {
        // Azure.Identity skriver også direkte til EventSource utenfor HTTP-pipelinen.
        // Undertrykk hele kategorien ved appens ILogger-grense, også når en SDK-lytter
        // videresender hendelser. Rå ETW/EventSource-lyttere er utenfor denne grensen.
        // PostConfigure gjelder også etter konfigurasjonsreload. Vern hver regel fordi
        // leverandørspesifikke regler ellers kan vinne over et generelt kategorifilter.
        builder.Services.PostConfigure<LoggerFilterOptions>(opsjoner =>
        {
            for (var i = 0; i < opsjoner.Rules.Count; i++)
            {
                var regel = opsjoner.Rules[i];
                var filter = regel.Filter;
                opsjoner.Rules[i] = new LoggerFilterRule(regel.ProviderName, regel.CategoryName, regel.LogLevel,
                    (leverandoer, kategori, nivaa) =>
                        kategori?.StartsWith("Azure.Identity", StringComparison.OrdinalIgnoreCase) != true
                        && (filter?.Invoke(leverandoer, kategori, nivaa) ?? true));
            }
            opsjoner.Rules.Add(new LoggerFilterRule(null, "Azure.Identity", LogLevel.None, null));
        });

        var konfigurasjon = builder.Configuration;
        var adresse = konfigurasjon["KeyVault:Uri"];
        if (adresse is null)
            return;

        if (!Uri.TryCreate(adresse, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.HostNameType != UriHostNameType.Dns
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/"
            || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new InvalidOperationException(
                "KeyVault:Uri må være en absolutt HTTPS-adresse til et hvelv uten brukerinfo, særskilt port, sti, spørring eller fragment.");
        }

        try
        {
            // Slå av HTTP-pipelinelogging og Activity-sporing på begge klientene.
            // Dette stanser ikke identitets-SDK-ens direkte EventSource-hendelser.
            klientopsjoner ??= new SecretClientOptions();
            SkjulDiagnostikk(klientopsjoner.Diagnostics);
            if (legitimasjon is null)
            {
                var identitetsopsjoner = new ManagedIdentityCredentialOptions(ManagedIdentityId.SystemAssigned);
                SkjulDiagnostikk(identitetsopsjoner.Diagnostics);
                legitimasjon = new ManagedIdentityCredential(identitetsopsjoner);
            }

            konfigurasjon.AddAzureKeyVault(new SecretClient(uri, legitimasjon, klientopsjoner),
                new AzureKeyVaultConfigurationOptions());
        }
        catch (Exception)
        {
            // Bevisst uten InnerException: også aggregert SDK-feil og identitetsfeil
            // kan ha sensitive verdier i melding, respons, header eller indre unntak.
            throw new InvalidOperationException(
                "Kunne ikke laste konfigurasjon fra Key Vault. Kontroller URI, nettverk, appens managed identity og tilgang til å liste og lese hemmeligheter.");
        }
    }

    private static void SkjulDiagnostikk(DiagnosticsOptions opsjoner)
    {
        opsjoner.IsLoggingEnabled = false;
        opsjoner.IsLoggingContentEnabled = false;
        opsjoner.IsDistributedTracingEnabled = false;
    }
}
