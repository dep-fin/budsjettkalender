using System.Net;
using System.Text.RegularExpressions;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public sealed class BudsjettaarHttpTester
{
    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(9999, 9999, 12, 31)]
    [InlineData(int.MinValue, 1, 1, 1)]
    [InlineData(int.MaxValue, 9999, 12, 31)]
    public async Task Hele_budsjettloepet_beholder_frist_ved_DateOnly_og_eldre_budsjettaarsgrenser(int budsjettaar, int aar, int maaned, int dag)
    {
        using var app = new TestApplikasjon();
        using var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Frister.RemoveRange(db.Frister);
        var id = Guid.NewGuid();
        db.Frister.Add(new()
        {
            Id = id, Tittel = "UX80 frist ved datogrense", Budsjettaar = budsjettaar,
            Dato = new(aar, maaned, dag), Kategori = Kategori.Regnskap,
            Synlighet = [new() { GruppeKode = "FA" }]
        });
        await db.SaveChangesAsync();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", "FA");

        var svar = await klient.GetAsync("/budsjettaar");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var html = await svar.Content.ReadAsStringAsync();
        Assert.Contains("UX80 frist ved datogrense", html);
        Assert.Contains($"data-frist-id=\"{id}\"", html);
        Assert.Contains($"value=\"{budsjettaar}\"", html);
        Assert.Contains("utenfor tidslinjen", html);
        Assert.DoesNotContain("Kunne ikke laste", html);
    }

    [Theory]
    [InlineData("2026-10-26T23:30:00+00:00", "2026-10-27", "27. oktober 2026")]
    [InlineData("2026-06-30T22:30:00+00:00", "2026-07-01", "1. juli 2026")]
    public async Task I_dag_paa_tidslinjen_bruker_Oslo_dato_etter_midnatt_ogsaa_i_sommertid(string utc, string dato, string datotekst)
    {
        using var app = new KlokkeApplikasjon(DateTimeOffset.Parse(utc));
        using var db = app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        db.Frister.Add(new()
        {
            Id = Guid.NewGuid(), Tittel = "UX80 dagens budsjettløp", Budsjettaar = 2026,
            Dato = new(2025, 11, 20), Synlighet = [new() { GruppeKode = "FA" }]
        });
        await db.SaveChangesAsync();
        using var klient = app.CreateClient();
        klient.DefaultRequestHeaders.Add("X-Test-Rolle", "Leser");
        klient.DefaultRequestHeaders.Add("X-Test-Grupper", "FA");

        var svar = await klient.GetAsync("/budsjettaar");

        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var html = WebUtility.HtmlDecode(await svar.Content.ReadAsStringAsync());
        Assert.Contains($"datetime=\"{dato}\"", html);
        Assert.Matches(new Regex("I dag[^<]*(?:<[^>]+>)*" + Regex.Escape(datotekst)), html);
    }

    private sealed class KlokkeApplikasjon(DateTimeOffset naa) : TestApplikasjon
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new Tidspunkt(naa)));
        }
    }

    private sealed class Tidspunkt(DateTimeOffset naa) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => naa;
    }
}
