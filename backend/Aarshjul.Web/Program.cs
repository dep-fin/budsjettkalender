using Aarshjul.Application.Brukere;
using Aarshjul.Application.Brukerforslag;
using Aarshjul.Application.Datautveksling;
using Aarshjul.Application.Datouttrekk;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Epost;
using Aarshjul.Infrastructure.Epost;
using Aarshjul.Application.Faser;
using Aarshjul.Application.Generering;
using Aarshjul.Application.Godkjenningsko;
using Aarshjul.Application.Grupper;
using Aarshjul.Application.Internkalender;
using Aarshjul.Application.Kalender;
using Aarshjul.Application.Opplasting;
using Aarshjul.Application.Synlighet;
using Aarshjul.Application.Utskrift;
using Aarshjul.Application.Varsler;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Brukere;
using Aarshjul.Infrastructure.Brukerforslag;
using Aarshjul.Infrastructure.Datautveksling;
using Aarshjul.Infrastructure.Datouttrekk;
using Aarshjul.Infrastructure.Frister;
using Aarshjul.Infrastructure.Faser;
using Aarshjul.Infrastructure.Generering;
using Aarshjul.Infrastructure.Godkjenningsko;
using Aarshjul.Infrastructure.Grupper;
using Aarshjul.Infrastructure.Internkalender;
using Aarshjul.Infrastructure.Kalender;
using Aarshjul.Infrastructure.Opplasting;
using Aarshjul.Infrastructure.Utskrift;
using Aarshjul.Infrastructure.Varsler;
using Aarshjul.Web.Api;
using Aarshjul.Web.Components;
using Aarshjul.Web.Demo;
using Aarshjul.Web.Konfigurasjon;
using Aarshjul.Web.Sikkerhet;
using Aarshjul.Web.Visning;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var app = await Program.OpprettAppAsync(WebApplication.CreateBuilder(args));
await app.RunAsync();

/// <summary>Felles oppstart for webverten og integrasjonstester.</summary>
public partial class Program
{
    /// <summary>Bygger samme webapp med utskiftbar legitimasjon og Azure-transport ved integrasjonstesting.</summary>
    public static async Task<WebApplication> OpprettAppAsync(
        WebApplicationBuilder builder, TokenCredential? keyVaultLegitimasjon = null,
        SecretClientOptions? keyVaultKlientopsjoner = null)
    {
        KeyVaultKonfigurasjon.Last(builder, keyVaultLegitimasjon, keyVaultKlientopsjoner);

        var erTesting = builder.Environment.IsEnvironment("Testing");
        // Demo-modus: lokal kjøring uten Azure SQL/Entra (SQLite + dev-innlogging + demo-data).
        // Rører ikke produksjonsstien. Aktiveres med konfig-flagget Demo=true (anbefalt, sammen med
        // miljøet Development slik at `dotnet run` serverer statiske assets riktig) ELLER med
        // ASPNETCORE_ENVIRONMENT=Demo (krever publiserte assets, dvs. `dotnet publish` + kjør DLL-en —
        // `MapStaticAssets` i et ikke-Development-miljø forventer publisert asset-layout).
        var erDemo = builder.Configuration.GetValue<bool>("Demo")
                     || builder.Environment.IsEnvironment("Demo");
        if (erDemo && !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Demo"))
            throw new InvalidOperationException("Demo er bare tillatt i Development- eller Demo-miljøet.");
        var brukEntra = !erTesting && !erDemo;

        // --- Database. Produksjon: Azure SQL. Demo: SQLite-fil. Test: registreres av testene. ---
        if (erDemo)
        {
            // Tom streng i appsettings teller som «ikke satt» → fil-DB (en tom DSN gir ellers en
            // privat temp-database per tilkobling, så skjemaet «forsvinner» mellom kall).
            var demoConn = builder.Configuration.GetConnectionString("Aarshjul");
            if (string.IsNullOrWhiteSpace(demoConn))
            {
                demoConn = "Data Source=aarshjul-demo.db";
            }
            builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(demoConn));
        }
        else if (!erTesting)
        {
            builder.Services.AddDbContextFactory<AppDbContext>(o =>
                o.UseSqlServer(builder.Configuration.GetConnectionString("Aarshjul")
                    ?? "Server=(localdb)\\mssqllocaldb;Database=Aarshjul;Trusted_Connection=True;"));
        }

        // --- Konfigurasjon ---
        builder.Services.Configure<EntraGruppeOpsjoner>(builder.Configuration.GetSection(EntraGruppeOpsjoner.Seksjon));
        builder.Services.Configure<AdministratortilgangOpsjoner>(builder.Configuration.GetSection(AdministratortilgangOpsjoner.Seksjon));
        builder.Services.Configure<StartdataOpsjoner>(builder.Configuration.GetSection(StartdataOpsjoner.Seksjon));
        builder.Services.Configure<SynlighetsregelOpsjoner>(builder.Configuration.GetSection(SynlighetsregelOpsjoner.Seksjon));

        // --- Tjenester ---
        builder.Services.AddSingleton(TimeProvider.System);
        var kontrollertEpost = (erDemo || erTesting) && builder.Configuration.GetValue<bool>("DagligEpost:KontrollertKjoring");
        if (kontrollertEpost)
        {
            builder.Services.AddSingleton<KontrollertEpostklokke>();
            builder.Services.AddSingleton<TimeProvider>(s => s.GetRequiredService<KontrollertEpostklokke>());
        }
        builder.Services.AddOptions<DagligEpostOpsjoner>().Bind(builder.Configuration.GetSection("DagligEpost"))
            .Validate(o => Uri.TryCreate(o.OffentligBasisadresse, UriKind.Absolute, out var uri)
                && (uri.Scheme == "https" || (erDemo || erTesting) && uri.Scheme == "http" && uri.IsLoopback),
                "DagligEpost:OffentligBasisadresse må være HTTPS, eller lokal loopback i demo/Testing.");
        builder.Services.AddScoped<IDagligEpostoppsummering, DagligEpostoppsummering>();
        // Testing oppretter skjema etter vertsstart. Kontrollerte klokker kjører eksplisitt.
        if (!erTesting && !kontrollertEpost) builder.Services.AddHostedService<Aarshjul.Web.Epost.DagligEpostjobb>();
        builder.Services.AddScoped<IEpostabonnement, EpostabonnementTjeneste>();
        builder.Services.AddScoped<IEpostabonnentadministrasjon, Epostabonnentadministrasjon>();
        builder.Services.AddScoped<EpostabonnentadministrasjonKommando>();
        builder.Services.AddScoped<EpostabonnementKommando>();
        if (erDemo || erTesting)
        {
            builder.Services.AddSingleton<KontrollertBekreftelsespost>();
            builder.Services.AddSingleton<IBekreftelsespost>(s => s.GetRequiredService<KontrollertBekreftelsespost>());
            builder.Services.AddSingleton<KontrollertOppsummeringspost>();
            builder.Services.AddSingleton<IOppsummeringspost>(s => s.GetRequiredService<KontrollertOppsummeringspost>());
            builder.Services.AddSingleton<KontrollertMottakertilgang>(s =>
            {
                var kilde = new KontrollertMottakertilgang();
                if (erDemo)
                    foreach (var p in Demodata.Personaer)
                        kilde.Sett(p.Id, new(MottakertilgangStatus.Aktiv,
                            new Synlighetskontekst(p.Rolle == Aarshjul.Domain.Funksjonsrolle.Administrator || p.Id == "demo-sbr", p.Grupper)));
                return kilde;
            });
            builder.Services.AddSingleton<IMottakertilgangskilde>(s => s.GetRequiredService<KontrollertMottakertilgang>());
        }
        else
        {
            builder.Services.AddSingleton<IBekreftelsespost, UtilgjengeligBekreftelsespost>();
            builder.Services.AddSingleton<IOppsummeringspost, UtilgjengeligOppsummeringspost>();
            builder.Services.AddSingleton<IMottakertilgangskilde, UtilgjengeligMottakertilgang>();
        }
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IFristlesing, FristTjeneste>();
        builder.Services.AddScoped<IFasemal, FasemalTjeneste>();
        builder.Services.AddScoped<FasemalKommando>();
        builder.Services.AddScoped<MaloverfoeringKommando>();
        builder.Services.AddScoped<FristoverfoeringKommando>();
        builder.Services.AddScoped<FristEksportutvalg>();
        builder.Services.AddScoped<EksportKommando>();
        builder.Services.AddScoped<GruppeadministrasjonKommando>();
        builder.Services.AddScoped<IFristskriving, FristskrivingTjeneste>();
        builder.Services.AddScoped<IGodkjenningsko, GodkjenningskoTjeneste>();
        builder.Services.AddScoped<IForslagsinnsending, ForslagsinnsendingTjeneste>();
        builder.Services.AddScoped<IVarseltjeneste, Varseltjeneste>();
        builder.Services.AddScoped<IGruppetjeneste, Gruppetjeneste>();
        builder.Services.AddScoped<IWordEksport, WordEksportTjeneste>();
        builder.Services.AddScoped<IKalenderEksport, IcsEksportTjeneste>();
        builder.Services.AddScoped<IKalenderabonnement, KalenderabonnementTjeneste>();
        builder.Services.AddSingleton<ISynlighetsregel, Synlighetsregel>();
        builder.Services.AddScoped<IGenereringstjeneste, GenereringsTjeneste>();
        builder.Services.AddScoped<IMaltjeneste, Maltjeneste>();
        builder.Services.AddScoped<IFristDatautveksling, FristDatautveksling>();
        builder.Services.AddScoped<IMalDatautveksling, MalDatautveksling>();
        // Internkalender for SBR (SBR-intern arbeidsliste).
        builder.Services.AddScoped<IInternkalender, InternkalenderTjeneste>();
        builder.Services.AddScoped<IGjoeremaalRegler, GjoeremaalRegelTjeneste>();
        builder.Services.AddScoped<InternkalenderKommando>();
        builder.Services.AddScoped<InternkalenderRetur>();
        // Datouttrekk + manuell dokumentopplasting (samme uttrekksledd som automatisk innhenting).
        // Heuristisk (offline) uttrekk nå; Claude-API-implementasjonen plugges inn bak IDatouttrekk (Steg E).
        builder.Services.AddSingleton<IPdftekst, PdftekstLeser>();
        builder.Services.AddSingleton<IDatouttrekk, HeuristiskDatouttrekk>();
        builder.Services.AddScoped<IDokumentopplasting, OpplastingsTjeneste>();
        builder.Services.AddScoped<IBrukeroppslag, BrukeroppslagTjeneste>();
        builder.Services.AddScoped<IAdministratoroversikt, AdministratoroversiktTjeneste>();
        builder.Services.AddScoped<AdministratoroversiktKommando>();
        builder.Services.AddScoped<ISynlighetskontekst, HttpSynlighetskontekst>();
        builder.Services.AddScoped<IClaimsTransformation, BrukerClaimsTransformation>();
        builder.Services.AddScoped<Synlighetskontekstkilde>();
        builder.Services.AddScoped<Gjeldendebrukerkilde>();
        builder.Services.AddScoped<Visningstilstand>();
        builder.Services.AddScoped<Kalendertilstand>();
        builder.Services.AddScoped<KoJusteringsretur>();
        builder.Services.AddScoped<Aarshjul.Web.Components.Pages.Admin.Mallistekontekst>();
        builder.Services.AddScoped<Aarshjul.Web.Components.Pages.Admin.Malproevekontekst>();

        // --- Autentisering. Produksjon: Entra ID (OIDC). Demo: cookie + dev-innlogging (ingen Entra).
        //     Test: injiseres av testene. ---
        if (brukEntra)
        {
            builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
            builder.Services.AddControllersWithViews().AddMicrosoftIdentityUI();
        }
        else if (erDemo)
        {
            builder.Services.AddAuthentication("Demo")
                .AddCookie("Demo", o =>
                {
                    o.LoginPath = "/demo";
                    o.AccessDeniedPath = "/demo";
                });
        }

        builder.Services.AddAuthorization(Autorisasjon.LeggTilPolicyer);

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        // En avvist JSON-POST skal beholde 401/403. HTML-feilsiden krever ellers
        // antiforgery ved ny kjøring av POST og kan erstatte avvisningen med 400.
        app.Use(async (http, neste) =>
        {
            if (http.Request.Path.StartsWithSegments("/api/admin/epostabonnenter")
                || http.Request.Path.StartsWithSegments("/admin/innstillinger/epostvarsling"))
            {
                http.Response.Headers.CacheControl = "no-store";
                if (http.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } abonnentstatus)
                    abonnentstatus.Enabled = false;
            }
            if (http.Request.Path.StartsWithSegments("/epostvarsling") || http.Request.Path == "/demo/epostmottaker")
            {
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers["Referrer-Policy"] = "no-referrer";
            }
            if (http.Request.Path.StartsWithSegments("/api/epostabonnement")
                && http.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } status)
                status.Enabled = false;
            if (http.Request.Path == "/api/epostabonnement/avmeldingslenke")
            {
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers["Referrer-Policy"] = "no-referrer";
            }
            await neste(http);
        });
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.MapFristEndepunkter();
        app.MapEpostEndepunkter();
        app.MapEpostabonnentEndepunkter();
        if (brukEntra)
        {
            app.MapControllers();
        }
        if (erDemo)
        {
            app.MapDemoEndepunkter();
        }

        // --- Migrasjon/skjema + seeding ved oppstart (testene styrer egen database). ---
        if (erDemo)
        {
            using var scope = app.Services.CreateScope();
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            // Ny SQLite-demo fra modellen; eksisterende EV02-demo oppgraderes additivt.
            await Demoskjema.SikreAsync(db);
            var opsjoner = scope.ServiceProvider.GetRequiredService<IOptions<StartdataOpsjoner>>().Value;
            await Startdata.SeedAsync(db, opsjoner);
            await Demodata.SeedAsync(db);
        }
        else if (!erTesting)
        {
            using var scope = app.Services.CreateScope();
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            db.Database.Migrate();
            var opsjoner = scope.ServiceProvider.GetRequiredService<IOptions<StartdataOpsjoner>>().Value;
            await Startdata.SeedAsync(db, opsjoner);
        }
        return app;
    }
}
