using Aarshjul.Application;
using Aarshjul.Application.Epost;
using Aarshjul.Web.Sikkerhet;

namespace Aarshjul.Web.Api;

public static class EpostdriftEndepunkter
{
    public static void MapEpostdriftEndepunkter(this IEndpointRouteBuilder app)
    {
        var gruppe = app.MapGroup("/api/admin/epostdrift").RequireAuthorization(Autorisasjon.ErAdministrator);
        gruppe.MapGet("", async (HttpContext http, IEpostdrift drift, CancellationToken ct) =>
        {
            if (http.User.Identity?.IsAuthenticated != true || EpostabonnentadministrasjonKommando.AktoerId(http.User) is null) return Results.Unauthorized();
            try { return Results.Ok(await drift.HentAsync(ct)); }
            catch (Exception) { return Results.Json(new { feil = "Driftsstatus kunne ikke lastes. Prøv igjen." }, statusCode: 503); }
        });
        gruppe.MapPost("/stans", async (HttpContext http, IEpostdrift drift, CancellationToken ct) =>
        {
            var aktoer = EpostabonnentadministrasjonKommando.AktoerId(http.User);
            if (http.User.Identity?.IsAuthenticated != true || aktoer is null) return Results.Unauthorized();
            if (!http.Request.HasJsonContentType()) return Results.Json(new { feil = "Handlingen krever JSON." }, statusCode: 415);
            if (http.Request.ContentLength > 4096) return Results.BadRequest(new { feil = "Handlingen kunne ikke behandles." });
            if (http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } grense) grense.MaxRequestBodySize = 4096;
            try
            {
                var inndata = await http.Request.ReadFromJsonAsync<Stans>(ct);
                if (inndata is null) return Results.BadRequest(new { feil = "Oppgi handling, versjon og uttrykkelig bekreftelse." });
                await drift.SettStansAsync(inndata.Stanset, inndata.Versjon, inndata.Bekreftet, aktoer, ct);
                return Results.Ok(new { melding = "Driftsinnstillingen er lagret." });
            }
            catch (System.Text.Json.JsonException) { return Results.BadRequest(new { feil = "Handlingen kunne ikke behandles." }); }
            catch (BadHttpRequestException) { return Results.BadRequest(new { feil = "Handlingen kunne ikke behandles." }); }
            catch (Valideringsfeil feil) { return Results.BadRequest(new { feil = feil.Message }); }
            catch (EpostabonnementKonflikt feil) { return Results.Conflict(new { feil = feil.Message }); }
            catch (Exception) { return Results.Json(new { feil = "Handlingen kunne ikke bekreftes. Oppdater driftsstatus før nytt forsøk." }, statusCode: 503); }
        });
    }
    public sealed record Stans(bool Stanset, Guid Versjon, bool Bekreftet);
}
