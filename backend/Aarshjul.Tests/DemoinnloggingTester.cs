using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Aarshjul.Tests;

public sealed class DemoinnloggingTester
{
    [Theory]
    [InlineData("Development", "true")]
    [InlineData("Demo", "false")]
    public async Task Begge_demomiljoer_viser_demolenke_og_logger_ut_cookien(string miljo, string flagg)
    {
        var tilkobling = $"Data Source=demo-innlogging-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        await using var holdAapen = new SqliteConnection(tilkobling);
        await holdAapen.OpenAsync();
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment(miljo).UseSetting("Demo", flagg)
            .UseSetting("ConnectionStrings:Aarshjul", tilkobling));
        using var klient = app.CreateClient(new() { AllowAutoRedirect = false });
        using var inn = await klient.GetAsync("/demo/logg-inn/demo-admin");
        Assert.Equal(HttpStatusCode.Redirect, inn.StatusCode);
        var side = await klient.GetStringAsync("/kalender");
        Assert.Contains("demobanner", side);
        Assert.Contains("href=\"/demo/logg-ut\" data-enhance-nav=\"false\"", side);
        using var ut = await klient.GetAsync("/demo/logg-ut");
        Assert.Equal(HttpStatusCode.Redirect, ut.StatusCode);
        using var skjermet = await klient.GetAsync("/api/frister");
        Assert.Equal(HttpStatusCode.Redirect, skjermet.StatusCode);
        Assert.StartsWith("http://localhost/demo", skjermet.Headers.Location!.ToString());
    }
}
