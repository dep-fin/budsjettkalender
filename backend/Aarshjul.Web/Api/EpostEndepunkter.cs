using Aarshjul.Application;
using Aarshjul.Application.Brukere;
using Aarshjul.Application.Epost;
using Aarshjul.Domain;

namespace Aarshjul.Web.Api;

public static class EpostEndepunkter
{
    public static void MapEpostEndepunkter(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/epostvarsling/avmeld", ["HEAD"], () => Results.Ok()).AllowAnonymous();
        // Denne ene anonyme inngangen gir bare avmeldingsrett. Les kroppen her,
        // slik at også ugyldig JSON får en generell feil uten modellbindingsdetaljer.
        app.MapPost("/api/epostabonnement/avmeldingslenke", async (HttpContext http, IEpostabonnement abonnement, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!http.Request.HasJsonContentType())
                return Results.Json(new { feil = "Avmelding krever en JSON-forespørsel." }, statusCode: StatusCodes.Status415UnsupportedMediaType);
            if (http.Request.ContentLength > 4096)
                return Results.BadRequest(new { feil = "Avmeldingen kunne ikke behandles." });
            if (http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } grense)
                grense.MaxRequestBodySize = 4096;
            try
            {
                var inndata = await http.Request.ReadFromJsonAsync<Avmeldingslenke>(ct);
                await abonnement.MeldAvMedLenkeAsync(inndata?.Token, ct);
                return Results.Ok(new { melding = "Hvis lenken gjelder dette abonnementet, er videre e-postvarsling stanset." });
            }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(new { feil = "Avmeldingen kunne ikke behandles." }); }
            catch (BadHttpRequestException) { return Results.BadRequest(new { feil = "Avmeldingen kunne ikke behandles." }); }
            catch (Exception) { return Results.Json(new { feil = "Avmeldingen kunne ikke behandles. Prøv igjen fra e-posten." }, statusCode: StatusCodes.Status503ServiceUnavailable); }
        }).AllowAnonymous();
        var gruppe = app.MapGroup("/api/epostabonnement").RequireAuthorization();
        // Også operasjonene uten nyttelast krever JSON. Dermed kan en fremmed
        // nettside ikke sende en enkel cookie-autentisert HTML-form-POST hit.
        // Appen tillater ikke CORS for disse inngangene. Blazor bruker kretskommandoen.
        gruppe.AddEndpointFilter(async (kontekst, neste) =>
        {
            if (HttpMethods.IsPost(kontekst.HttpContext.Request.Method)
                && !kontekst.HttpContext.Request.HasJsonContentType())
                return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
            return await neste(kontekst);
        });
        gruppe.MapGet("", async (HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var bruker = await brukere.HentEllerOpprettAsync(http.User, ct);
            return Results.Ok(await abonnement.HentAsync(bruker.Id, ct));
        });
        gruppe.MapPost("", async (Paamelding inndata, HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var bruker = await brukere.HentEllerOpprettAsync(http.User, ct);
            try { return Results.Ok(await abonnement.MeldPaaAsync(bruker.Id, inndata.Adresse, inndata.GodtarStandardvalg, inndata.NyeFrister, inndata.VesentligeEndringer, inndata.Budsjettaar, inndata.Kategorier, ct)); }
            catch (Valideringsfeil feil) { return Results.BadRequest(new { feil = feil.Message }); }
            catch (EpostabonnementKonflikt feil) { return Results.Conflict(new { feil = feil.Message }); }
        });
        gruppe.MapPost("/bekreft", (Bekreftelse inndata, HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct)
            => EndreAsync(http, brukere, id => abonnement.BekreftAsync(id, inndata.Token, ct), ct));
        gruppe.MapPost("/ny-lenke", (HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct)
            => EndreAsync(http, brukere, id => abonnement.SendNyAsync(id, ct), ct));
        gruppe.MapPost("/meld-av", (HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct)
            => EndreAsync(http, brukere, id => abonnement.MeldAvAsync(id, ct), ct));
        gruppe.MapPost("/valg", (Varselvalg inndata, HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct)
            => EndreAsync(http, brukere, id => abonnement.EndreValgAsync(id, inndata.NyeFrister, inndata.VesentligeEndringer, inndata.Versjon, inndata.Budsjettaar, inndata.Kategorier, ct), ct));
        gruppe.MapPost("/adresse", (Adressebytte inndata, HttpContext http, IBrukeroppslag brukere, IEpostabonnement abonnement, CancellationToken ct)
            => EndreAsync(http, brukere, id => abonnement.EndreAdresseAsync(id, inndata.Adresse, inndata.EgenadresseSamtykke, inndata.Versjon, ct), ct));
    }

    private static async Task<IResult> EndreAsync(HttpContext http, IBrukeroppslag brukere,
        Func<string, Task<EpostabonnementDto>> handling, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var bruker = await brukere.HentEllerOpprettAsync(http.User, ct);
        try { return Results.Ok(await handling(bruker.Id)); }
        catch (Valideringsfeil feil) { return Results.BadRequest(new { feil = feil.Message }); }
        catch (EpostabonnementKonflikt feil) { return Results.Conflict(new { feil = feil.Message }); }
    }
    public sealed record Paamelding(string Adresse, bool GodtarStandardvalg, bool NyeFrister = true, bool VesentligeEndringer = false,
        int[]? Budsjettaar = null, Kategori[]? Kategorier = null);
    public sealed record Varselvalg(bool NyeFrister, bool VesentligeEndringer, Guid Versjon,
        int[]? Budsjettaar = null, Kategori[]? Kategorier = null);
    public sealed record Adressebytte(string Adresse, bool EgenadresseSamtykke, Guid Versjon);
    public sealed class Bekreftelse { public string Token { get; init; } = ""; }
    public sealed class Avmeldingslenke { public string? Token { get; init; } }
}
