using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Net;
using System.Text;
using System.Text.Json;
using Aarshjul.Infrastructure;
using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aarshjul.Tests;

[Collection("Key Vault-oppstart")]
public sealed class KeyVaultOppstartTester
{
    [Fact]
    public void Ekte_managed_identity_feil_nar_ikke_appens_loggsinker_selv_med_aktiv_SDK_videresending()
    {
        using var miljo = new Identitetsmiljo();
        var logger = new ConcurrentQueue<string>();
        // En ferdigbygd vert gir appens faktiske ILoggerFactory med produksjonens
        // filteroppsett. Feilet konfigurasjonslasting skjer før verten kan bygges.
        using var utenHvelv = new KeyVaultSvar([]);
        using var loggvert = new KeyVaultApplikasjon("Testing", new Dictionary<string, string?>
        {
            ["KeyVault:Uri"] = null,
            ["Logging:LogLevel:Azure.Identity"] = "Trace",
            ["Logging:Oppstartstest:LogLevel:Azure.Identity"] = "Trace"
        }, utenHvelv, logger, tilpassBygger: builder =>
            builder.Logging.AddFilter("Aarshjul.Skjult", _ => false));
        var loggfabrikk = loggvert.Services.GetRequiredService<ILoggerFactory>();
        var oppstartslogger = loggfabrikk.CreateLogger("Aarshjul.Web.Oppstart");
        oppstartslogger.LogWarning("Syntetisk kontroll: appens øvrige logging fungerer.");
        loggfabrikk.CreateLogger("Azure.Identity").LogCritical("syntetisk-foelsom-foer-reload");
        loggfabrikk.CreateLogger("Aarshjul.Skjult").LogCritical("skal-fortsatt-vaere-skjult");
        var konfigurasjon = loggvert.Services.GetRequiredService<IConfiguration>();
        konfigurasjon["Logging:Oppstartstest:LogLevel:Azure.Identity"] = "Trace";
        ((IConfigurationRoot)konfigurasjon).Reload();

        var raaSdkLogger = new ConcurrentQueue<(string? Hendelse, string Tekst)>();
        using var azureLogger = new AzureEventSourceListener((hendelse, tekst) =>
        {
            raaSdkLogger.Enqueue((hendelse.EventName, tekst));
            // Samme kategorimapping som Microsofts AzureEventSourceLogForwarder.
            // Error brukes også for Info/Debug: vernet må gjelde alle loggnivåer.
            loggfabrikk.CreateLogger(hendelse.EventSource.Name.Replace('-', '.'))
                .LogError(new EventId(hendelse.EventId, hendelse.EventName), "{SdkHendelse}", tekst);
        }, EventLevel.Verbose);
        using var identitetssvar = new Identitetssvar();
        using var identitetsklient = new HttpClient(identitetssvar);
        var identitetsopsjoner = new ManagedIdentityCredentialOptions(ManagedIdentityId.SystemAssigned)
        {
            Transport = new HttpClientTransport(identitetsklient),
            Retry = { MaxRetries = 0 },
            Diagnostics =
            {
                IsLoggingEnabled = false,
                IsLoggingContentEnabled = false,
                IsDistributedTracingEnabled = false
            }
        };
        using var hvelv = new KeyVaultSvar([]);
        using var app = new KeyVaultApplikasjon("Testing", new Dictionary<string, string?>
        {
            ["KeyVault:Uri"] = "https://test.vault.azure.net/"
        }, hvelv, logger, legitimasjon: new ManagedIdentityCredential(identitetsopsjoner));

        var feil = Assert.Throws<InvalidOperationException>(() => _ = app.Services);
        oppstartslogger.LogError(feil, "Oppstart ble stanset.");

        Assert.StartsWith("Kunne ikke laste konfigurasjon fra Key Vault.", feil.Message);
        Assert.Null(feil.InnerException);
        Assert.True(identitetssvar.AntallForesporsler > 0);
        // Rå EventSource/ETW er en privilegert diagnosegrense som appens ILogger-filter
        // ikke kan styre. Bevis at ekte SDK-sti kjører og fortsatt kan røpe responsen der.
        Assert.Contains(raaSdkLogger, x => x.Hendelse == "LogMsalError" && x.Tekst.Contains("syntetisk-foelsom-identitetsrespons", StringComparison.Ordinal));
        Assert.Contains(raaSdkLogger, x => x.Hendelse == "GetTokenFailed" && x.Tekst.Contains("syntetisk-foelsom-identitetsrespons", StringComparison.Ordinal));
        Assert.Contains(logger, tekst => tekst.Contains("appens øvrige logging fungerer", StringComparison.Ordinal));
        Assert.Contains(logger, tekst => tekst.Contains("Oppstart ble stanset.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger, tekst => tekst.Contains("syntetisk-foelsom", StringComparison.Ordinal));
        Assert.DoesNotContain(logger, tekst => tekst.Contains("skal-fortsatt-vaere-skjult", StringComparison.Ordinal));
    }

    [Fact]
    public void KeyVault_overstyrer_json_miljo_og_kommandolinje_foer_Entra_binder_verdiene()
    {
        const string klientId = "26b84874-e434-4cbb-8e83-5ede176945fd";
        var miljoPrefiks = $"AARSHJUL_KEYVAULT_TEST_{Guid.NewGuid():N}_";
        Environment.SetEnvironmentVariable(miljoPrefiks + "AzureAd__ClientId", "fra-miljo");
        try
        {
            var logger = new ConcurrentQueue<string>();
            using var azureLogger = new AzureEventSourceListener((_, tekst) => logger.Enqueue(tekst), EventLevel.Verbose);
            using var kilde = new KeyVaultSvar(new Dictionary<string, string>
            {
                ["AzureAd--ClientId"] = klientId,
                ["azuread--CLIENTsecret"] = "syntetisk-foelsom-klienthemmelighet"
            });
            var tilkobling = $"Data Source=entra-keyvault-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
            using var holdAapen = new SqliteConnection(tilkobling);
            holdAapen.Open();
            var databaseopsjoner = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(tilkobling, sqlite => sqlite.MigrationsAssembly(typeof(KeyVaultOppstartTester).Assembly.FullName))
                .Options;
            using (var db = new AppDbContext(databaseopsjoner))
                db.Database.EnsureCreated();

            using var app = new KeyVaultApplikasjon("Production", new Dictionary<string, string?>
            {
                ["KeyVault:Uri"] = "https://test.vault.azure.net",
                ["AzureAd:TenantId"] = "203ef24e-c048-4589-954f-97212c2a2575",
                ["AzureAd:Domain"] = "syntetisk.onmicrosoft.com"
            }, kilde, logger, tilpassBygger: builder =>
            {
                builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(
                    "{\"AzureAd\":{\"ClientId\":\"fra-json\",\"ClientSecret\":\"fra-json\"}}")));
                Assert.Equal("fra-json", builder.Configuration["AzureAd:ClientId"]);
                builder.Configuration.AddEnvironmentVariables(miljoPrefiks);
                Assert.Equal("fra-miljo", builder.Configuration["AzureAd:ClientId"]);
                builder.Configuration.AddCommandLine(["--AzureAd:ClientId=fra-kommandolinje", "--AzureAd:ClientSecret=fra-kommandolinje"]);
                Assert.Equal("fra-kommandolinje", builder.Configuration["AzureAd:ClientId"]);
                // Bare den eksterne databasen byttes. Entra og resten av oppstarten kjøres.
                builder.Host.ConfigureContainer<IServiceCollection>((_, tjenester) =>
                {
                    tjenester.RemoveAll<IDbContextFactory<AppDbContext>>();
                    tjenester.AddSingleton<IDbContextFactory<AppDbContext>>(new SqliteFabrikk(databaseopsjoner));
                });
            });

            var entra = app.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme);
            Assert.Equal(klientId, entra.ClientId);
            Assert.Equal("syntetisk-foelsom-klienthemmelighet", entra.ClientSecret);
            Assert.Contains("203ef24e-c048-4589-954f-97212c2a2575", entra.Authority);
            Assert.Equal("syntetisk.onmicrosoft.com", app.Services.GetRequiredService<IConfiguration>()["AzureAd:Domain"]);
            Assert.DoesNotContain(logger, tekst => tekst.Contains("syntetisk-foelsom", StringComparison.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable(miljoPrefiks + "AzureAd__ClientId", null);
        }
    }

    [Theory]
    [InlineData("Testing", "false")]
    [InlineData("Development", "true")]
    [InlineData("Demo", "false")]
    public void Uten_uri_starter_lokale_miljoer_uten_eksterne_kall(string miljo, string demo)
    {
        var tilkobling = $"Data Source=keyvault-offline-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        using var holdAapen = new SqliteConnection(tilkobling);
        holdAapen.Open();
        using var kilde = new KeyVaultSvar([], "nettverk");
        using var app = new KeyVaultApplikasjon(miljo, new Dictionary<string, string?>
        {
            ["Demo"] = demo,
            ["KeyVault:Uri"] = null,
            ["ConnectionStrings:Aarshjul"] = tilkobling
        }, kilde);

        Assert.NotNull(app.Services);
        Assert.Equal(0, kilde.AntallForesporsler);
    }

    [Theory]
    [InlineData("ikke-en-uri")]
    [InlineData("https://bruker:syntetisk-passord@test.vault.azure.net/?token=syntetisk-token")]
    public void Normal_inngang_bruker_samme_trygge_uri_validering(string uri)
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("KeyVault:Uri", uri));

        var feil = Assert.Throws<InvalidOperationException>(() => _ = app.Services);
        Assert.StartsWith("KeyVault:Uri må være en absolutt HTTPS-adresse", feil.Message);
        Assert.Null(feil.InnerException);
        Assert.DoesNotContain("syntetisk", feil.ToString());
    }

    [Theory]
    [InlineData("liste-403")]
    [InlineData("henting-500")]
    [InlineData("nettverk")]
    [InlineData("legitimasjon")]
    public void Lastingsfeil_stanser_oppstart_uten_lekkasje_i_feil_eller_logger(string feiltype)
    {
        var logger = new ConcurrentQueue<string>();
        using var azureLogger = new AzureEventSourceListener((_, tekst) => logger.Enqueue(tekst), EventLevel.Verbose);
        using var kilde = new KeyVaultSvar(new Dictionary<string, string> { ["AzureAd--ClientSecret"] = "syntetisk-foelsom-verdi" }, feiltype);
        using var app = new KeyVaultApplikasjon("Testing", new Dictionary<string, string?>
        {
            ["KeyVault:Uri"] = "https://test.vault.azure.net/",
            ["AzureAd:ClientSecret"] = "fra-lokal-konfigurasjon"
        }, kilde, logger, legitimasjon: new SyntetiskLegitimasjon(feiltype == "legitimasjon"));

        var feil = Assert.Throws<InvalidOperationException>(() => _ = app.Services);
        Assert.StartsWith("Kunne ikke laste konfigurasjon fra Key Vault.", feil.Message);
        Assert.Null(feil.InnerException);
        Assert.DoesNotContain("syntetisk-foelsom", feil.ToString());
        Assert.DoesNotContain(logger, tekst => tekst.Contains("syntetisk-foelsom", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ikke-en-uri")]
    [InlineData("/relativ/sti")]
    [InlineData("http://test.vault.azure.net/")]
    [InlineData("https://bruker:syntetisk-passord@test.vault.azure.net/")]
    [InlineData("https://test.vault.azure.net/?token=syntetisk-token")]
    [InlineData("https://test.vault.azure.net/#syntetisk-fragment")]
    [InlineData("https://test.vault.azure.net/secrets/hemmelighet")]
    [InlineData("https://test.vault.azure.net:8443/")]
    public void Ugyldig_eksplisitt_uri_avvises_trygt_foer_lasting(string uri)
    {
        var logger = new ConcurrentQueue<string>();
        using var azureLogger = new AzureEventSourceListener((_, tekst) => logger.Enqueue(tekst), EventLevel.Verbose);
        using var kilde = new KeyVaultSvar([]);
        using var app = new KeyVaultApplikasjon("Testing", new Dictionary<string, string?> { ["KeyVault:Uri"] = uri }, kilde, logger);

        var feil = Assert.Throws<InvalidOperationException>(() => _ = app.Services);
        Assert.StartsWith("KeyVault:Uri må være en absolutt HTTPS-adresse", feil.Message);
        Assert.Null(feil.InnerException);
        Assert.DoesNotContain("syntetisk", feil.ToString());
        Assert.DoesNotContain(logger, tekst => tekst.Contains("syntetisk", StringComparison.Ordinal));
        Assert.Equal(0, kilde.AntallForesporsler);
    }

    [Fact]
    public void Hemmeligheter_lastes_foer_demodatabasen_konfigureres()
    {
        using var holdAapen = new SqliteConnection("Data Source=keyvault-oppstart;Mode=Memory;Cache=Shared");
        holdAapen.Open();
        using var kilde = new KeyVaultSvar(new Dictionary<string, string>
        {
            ["ConnectionStrings--Aarshjul"] = "Data Source=keyvault-oppstart;Mode=Memory;Cache=Shared"
        });
        using var app = new KeyVaultApplikasjon("Development", new Dictionary<string, string?>
        {
            ["Demo"] = "true",
            ["KeyVault:Uri"] = "https://test.vault.azure.net/",
            ["ConnectionStrings:Aarshjul"] = "Ugyldig lokal tilkobling"
        }, kilde);

        using var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        Assert.Equal("Data Source=keyvault-oppstart;Mode=Memory;Cache=Shared", db.Database.GetConnectionString());
        Assert.NotEmpty(db.Synlighetsgrupper);
    }

    // WebApplicationFactorys sene ConfigureAppConfiguration kan ikke endre konfigurasjonen
    // som minimal hosting allerede har lest. Vi starter samme komposisjonsrot som Main,
    // med kontrollerte konfigurasjonskilder og en ekte Azure-klient mot lokal transport.
    private sealed class KeyVaultApplikasjon(
        string miljo, Dictionary<string, string?> konfigurasjon, HttpMessageHandler transport,
        ConcurrentQueue<string>? logger = null, Action<WebApplicationBuilder>? tilpassBygger = null,
        TokenCredential? legitimasjon = null)
        : WebApplicationFactory<Program>
    {
        private readonly HttpClient _httpKlient = new(transport, disposeHandler: false);

        protected override IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder();

        protected override IHost CreateHost(IHostBuilder vert)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(Program).Assembly.GetName().Name,
                EnvironmentName = miljo,
                ContentRootPath = Path.GetFullPath("../../../../Aarshjul.Web", AppContext.BaseDirectory)
            });
            builder.Configuration.AddInMemoryCollection(konfigurasjon);
            builder.Services.AddAuthentication();
            if (logger is not null)
                builder.Logging.AddProvider(new Testlogger(logger));
            builder.WebHost.UseTestServer();
            tilpassBygger?.Invoke(builder);
            var klientopsjoner = new SecretClientOptions
            {
                Transport = new HttpClientTransport(_httpKlient),
                Retry = { MaxRetries = 0 }
            };
            WebApplication? app = null;
            try
            {
                app = Program.OpprettAppAsync(builder, legitimasjon ?? new SyntetiskLegitimasjon(), klientopsjoner).GetAwaiter().GetResult();
                app.Start();
                return app;
            }
            catch
            {
                if (app is not null)
                    app.DisposeAsync().AsTask().GetAwaiter().GetResult();
                else
                    ((IDisposable)builder.Configuration).Dispose();
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _httpKlient.Dispose();
        }
    }

    private sealed class SyntetiskLegitimasjon(bool feil = false) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            if (feil)
                throw new AuthenticationFailedException("syntetisk-foelsom-identitetsfeil", new IOException("syntetisk-foelsom-indre-feil"));
            return new("syntetisk-testtoken", DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class KeyVaultSvar(Dictionary<string, string> hemmeligheter, string? feiltype = null) : HttpMessageHandler
    {
        private int _antallForesporsler;
        public int AntallForesporsler => _antallForesporsler;
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Svar(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Svar(request));

        private HttpResponseMessage Svar(HttpRequestMessage request)
        {
            Interlocked.Increment(ref _antallForesporsler);
            if (feiltype == "nettverk")
                throw new HttpRequestException("syntetisk-foelsom-nettverksfeil");
            var deler = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (request.Headers.Authorization is null)
            {
                var utfordring = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                utfordring.Headers.WwwAuthenticate.ParseAdd(
                    "Bearer authorization=\"https://login.microsoftonline.com/203ef24e-c048-4589-954f-97212c2a2575/oauth2/authorize\", resource=\"https://vault.azure.net\"");
                return utfordring;
            }
            if (feiltype == "liste-403" || (feiltype == "henting-500" && deler.Length > 1))
            {
                return new HttpResponseMessage(feiltype == "liste-403" ? HttpStatusCode.Forbidden : HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":{\"code\":\"Forbidden\",\"message\":\"syntetisk-foelsom-leverandoerfeil\"}}", Encoding.UTF8, "application/json")
                };
            }
            object innhold = deler.Length == 1
                ? new { value = hemmeligheter.Keys.Select(navn => new { id = $"https://test.vault.azure.net/secrets/{navn}", attributes = new { enabled = true } }) }
                : new { id = $"https://test.vault.azure.net/secrets/{deler[1]}/versjon", value = hemmeligheter[deler[1]], attributes = new { enabled = true } };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(innhold), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SqliteFabrikk(DbContextOptions<AppDbContext> opsjoner) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(opsjoner);
    }

    [ProviderAlias("Oppstartstest")]
    private sealed class Testlogger(ConcurrentQueue<string> logger) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Skriver(logger);
        public void Dispose() { }

        private sealed class Skriver(ConcurrentQueue<string> logger) : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logger.Enqueue(formatter(state, exception) + exception);
        }
    }

    private sealed class Identitetssvar : HttpMessageHandler
    {
        private int _antallForesporsler;
        public int AntallForesporsler => _antallForesporsler;
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Svar();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Svar());
        private HttpResponseMessage Svar()
        {
            Interlocked.Increment(ref _antallForesporsler);
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":\"invalid_request\",\"error_description\":\"syntetisk-foelsom-identitetsrespons\"}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class Identitetsmiljo : IDisposable
    {
        private readonly Dictionary<string, string?> _tidligere = new[]
        {
            "IDENTITY_ENDPOINT", "IDENTITY_HEADER", "IDENTITY_SERVER_THUMBPRINT",
            "MSI_ENDPOINT", "MSI_SECRET", "IMDS_ENDPOINT", "AZURE_POD_IDENTITY_AUTHORITY_HOST"
        }.ToDictionary(navn => navn, Environment.GetEnvironmentVariable);

        public Identitetsmiljo()
        {
            foreach (var navn in _tidligere.Keys)
                Environment.SetEnvironmentVariable(navn, null);
            Environment.SetEnvironmentVariable("IDENTITY_ENDPOINT", "http://127.0.0.1:1/identitet");
            Environment.SetEnvironmentVariable("IDENTITY_HEADER", "syntetisk-header");
        }

        public void Dispose()
        {
            foreach (var (navn, verdi) in _tidligere)
                Environment.SetEnvironmentVariable(navn, verdi);
        }
    }
}

// SDK-en velger identitetskilde fra prosessmiljøet. Isoler hele samlingen fra alle
// andre testklasser og gjenopprett alltid variablene etter hvert syntetiske forsøk.
[CollectionDefinition("Key Vault-oppstart", DisableParallelization = true)]
public sealed class KeyVaultOppstartSamling;
