using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aarshjul.Tests;

public class OppstartssikkerhetTester
{
    [Fact]
    public void Produksjon_avviser_demoinnlogging()
    {
        using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").UseSetting("Demo", "true"));

        var feil = Assert.Throws<InvalidOperationException>(() => _ = app.Services);
        Assert.Contains("Demo er bare tillatt", feil.Message);
    }
}
