using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Web.Sikkerhet;

namespace Aarshjul.Web.Api;

public static class EpostabonnentEndepunkter
{
    public static void MapEpostabonnentEndepunkter(this IEndpointRouteBuilder app)
    {
        var gruppe = app.MapGroup("/api/admin/epostabonnenter").RequireAuthorization(Autorisasjon.ErAdministrator);
        gruppe.MapGet("", async (HttpContext http, IEpostabonnentadministrasjon tjeneste, CancellationToken ct) =>
        {
            if (http.User.Identity?.IsAuthenticated != true || EpostabonnentadministrasjonKommando.AktoerId(http.User) is null)
                return Results.Unauthorized();
            try { return Results.Ok(await tjeneste.HentAsync(ct)); }
            catch (Exception) { return Results.Json(new { feil = "Abonnentene kunne ikke lastes. Prøv igjen." }, statusCode: 503); }
        });
        gruppe.MapPost("/stans", async (HttpContext http, IEpostabonnentadministrasjon tjeneste, CancellationToken ct) =>
        {
            var aktoer = EpostabonnentadministrasjonKommando.AktoerId(http.User);
            if (http.User.Identity?.IsAuthenticated != true || aktoer is null) return Results.Unauthorized();
            if (!http.Request.HasJsonContentType())
                return Results.Json(new { feil = "Stans krever en JSON-forespørsel." }, statusCode: 415);
            if (http.Request.ContentLength > 4096) return Results.BadRequest(new { feil = "Stans kunne ikke behandles." });
            if (http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } grense)
                grense.MaxRequestBodySize = 4096;
            try
            {
                var inndata = await http.Request.ReadFromJsonAsync<Stans>(ct);
                if (inndata is null) return Results.BadRequest(new { feil = "Oppgi abonnent, versjon og uttrykkelig bekreftelse." });
                await tjeneste.StansAsync(inndata.BrukerId, inndata.Versjon, inndata.Bekreftet, aktoer, ct);
                return Results.Ok(new { melding = "Videre e-postvarsling er stanset." });
            }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(new { feil = "Stans kunne ikke behandles." }); }
            catch (BadHttpRequestException) { return Results.BadRequest(new { feil = "Stans kunne ikke behandles." }); }
            catch (Valideringsfeil feil) { return Results.BadRequest(new { feil = feil.Message }); }
            catch (EpostabonnementKonflikt feil) { return Results.Conflict(new { feil = feil.Message }); }
            catch (Exception) { return Results.Json(new { feil = "Stans kunne ikke bekreftes. Oppdater abonnentene før du prøver igjen." }, statusCode: 503); }
        });
    }
    public sealed record Stans(string BrukerId, Guid Versjon, bool Bekreftet);
}
