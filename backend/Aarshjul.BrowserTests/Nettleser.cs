using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Aarshjul.BrowserTests;

// Én disponibel app/database per scenario. Bare demo-identiteten byttes; UI og server er reelle.
internal sealed class Nettleser
{
    private Process _app;
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly ConcurrentQueue<string> _feil = new();
    private readonly List<IBrowserContext> _kontekster = [];
    private readonly ConcurrentQueue<string> _applogg;
    public string Mappe { get; }
    public string Adresse { get; }
    public IPage Side { get; set; } = null!;

    private Nettleser(Process app, IPlaywright playwright, IBrowser browser, string mappe, string adresse, ConcurrentQueue<string> applogg)
        => (_app, _playwright, _browser, Mappe, Adresse, _applogg) = (app, playwright, browser, mappe, adresse, applogg);

    public static async Task Kjor(Func<Nettleser, Task> scenario)
    {
        var n = await Start();
        try { await scenario(n); }
        catch (Exception scenariofeil)
        {
            try { await n.RyddOpp(); }
            catch (Exception oppryddingsfeil)
            {
                throw new AggregateException("Scenario og opprydding av nettlesertesten feilet.", scenariofeil, oppryddingsfeil);
            }
            throw;
        }
        await n.RyddOpp();
    }

    private static async Task<Nettleser> Start()
    {
        var rot = new DirectoryInfo(AppContext.BaseDirectory);
        while (rot is not null && !File.Exists(Path.Combine(rot.FullName, "global.json"))) rot = rot.Parent;
        if (rot is null) throw new InvalidOperationException("Fant ikke repo-roten.");
        var web = Path.Combine(rot.FullName, "backend", "Aarshjul.Web");
        var dll = Path.Combine(web, "bin", "Release", "net10.0", "Aarshjul.Web.dll");
        if (!File.Exists(dll)) throw new InvalidOperationException("Bygg Web i Release før nettlesertestene.");
        var mappe = Path.Combine(Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_EVIDENCE")
            ?? Path.Combine(Path.GetTempPath(), "aarshjul-browser"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mappe);
        var port = new TcpListener(IPAddress.Loopback, 0);
        port.Start();
        var adresse = $"http://127.0.0.1:{((IPEndPoint)port.LocalEndpoint).Port}";
        port.Stop();
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("AARSHJUL_DOTNET") ?? "dotnet")
        {
            WorkingDirectory = web, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { dll, "--urls", adresse }) start.ArgumentList.Add(arg);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["Demo"] = "true";
        start.Environment["ConnectionStrings__Aarshjul"] = $"Data Source={Path.Combine(mappe, "demo.db")}";
        // Ikke skriv bearer-URL-er fra feed-forespørsler til CI-loggen.
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        var logg = new ConcurrentQueue<string>();
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        Nettleser? test = null;
        var app = Process.Start(start) ?? throw new InvalidOperationException("Kunne ikke starte demo.");
        try
        {
            app.OutputDataReceived += (_, e) => { if (e.Data is not null) logg.Enqueue(e.Data); };
            app.ErrorDataReceived += (_, e) => { if (e.Data is not null) logg.Enqueue(e.Data); };
            app.BeginOutputReadLine(); app.BeginErrorReadLine();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            while (true)
            {
                if (app.HasExited) throw new InvalidOperationException("Demo avsluttet under oppstart.");
                try { if ((await http.GetAsync(adresse + "/demo", frist.Token)).IsSuccessStatusCode) break; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!frist.IsCancellationRequested) { }
                await Task.Delay(100, frist.Token); // Avgrenset readiness-poll, ingen nettlesersøvn.
            }
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new()
            {
                Headless = true, Channel = Environment.GetEnvironmentVariable("AARSHJUL_BROWSER_CHANNEL")
            });
            test = new Nettleser(app, playwright, browser, mappe, adresse, logg);
            await test.LoggInn(null);
            return test;
        }
        catch (Exception oppstartsfeil)
        {
            try { await RyddOpp(app, playwright, browser, test?._kontekster ?? [], mappe, logg); }
            catch (Exception oppryddingsfeil)
            {
                throw new AggregateException("Oppstart og opprydding av nettlesertesten feilet.", oppstartsfeil, oppryddingsfeil);
            }
            throw;
        }
    }

    public async Task<IPage> NySide()
    {
        var ctx = await _browser.NewContextAsync(new() { BaseURL = Adresse, Locale = "nb-NO", TimezoneId = "Europe/Oslo" });
        _kontekster.Add(ctx);
        ctx.Page += (_, side) =>
        {
            side.SetDefaultTimeout(10_000);
            side.PageError += (_, e) => _feil.Enqueue(RedigerHemmelighet(e));
            side.Console += (_, e) => { if (e.Type == "error") _feil.Enqueue(RedigerHemmelighet(e.Location + " " + e.Text)); };
        };
        return await ctx.NewPageAsync();
    }

    public async Task LoggInn(string? persona)
    {
        Side = await NySide(); // Ny cookiebeholder og full lasting ved hvert personabytte.
        await Side.GotoAsync(persona is null ? "/demo" : $"/demo/logg-inn/{persona}");
    }

    // Starter bare scenarioets egen prosess på nytt. Adresse og SQLite-fil er de samme.
    public async Task OmstartAsync()
    {
        foreach (var kontekst in _kontekster) await kontekst.CloseAsync();
        _kontekster.Clear();
        var start = _app.StartInfo;
        if (!_app.HasExited) _app.Kill(entireProcessTree: true);
        await _app.WaitForExitAsync();
        _app.Dispose();
        _app = Process.Start(start) ?? throw new InvalidOperationException("Kunne ikke starte scenarioets app på nytt.");
        _app.OutputDataReceived += (_, e) => { if (e.Data is not null) _applogg.Enqueue(e.Data); };
        _app.ErrorDataReceived += (_, e) => { if (e.Data is not null) _applogg.Enqueue(e.Data); };
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        using var frist = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (true)
        {
            if (_app.HasExited) throw new InvalidOperationException("Scenarioets app avsluttet under omstart.");
            try { if ((await http.GetAsync(Adresse + "/demo", frist.Token)).IsSuccessStatusCode) return; }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!frist.IsCancellationRequested) { }
            await Task.Delay(100, frist.Token);
        }
    }

    public async Task Gaa(string sti, IPage? side = null)
    {
        side ??= Side;
        await side.GotoAsync(sti);
        await side.Locator("h1").WaitForAsync();
        await Expect(side.Locator("[data-interaktiv]")).ToHaveAttributeAsync("data-interaktiv", "klar");
        await side.Locator(".melding").Filter(new() { HasText = "Laster …" }).WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    public async Task<JsonArray> Frister(bool historikk = true)
    {
        var svar = await Side.Context.APIRequest.GetAsync("/api/frister" + (historikk ? "?historikk=true" : ""));
        Assert.Equal(200, svar.Status);
        return JsonNode.Parse(await svar.TextAsync())!.AsArray();
    }

    public async Task OpprettFrist(string tittel, string gruppe = "FA", string? tid = null, bool tentativ = false,
        string dato = "2030-11-20", int budsjettaar = 2031, string? notat = null, string? kategori = null,
        string kvalifikator = "Medio")
    {
        await Gaa("/admin/frist/ny");
        await Side.GetByLabel("Tittel", new() { Exact = true }).FillAsync(tittel);
        await Side.GetByLabel("Dato", new() { Exact = true }).FillAsync(dato);
        await Side.GetByLabel("Budsjettår", new() { Exact = true }).FillAsync(budsjettaar.ToString());
        if (notat is not null) await Side.GetByLabel("Notat", new() { Exact = true }).FillAsync(notat);
        if (kategori is not null) await Side.GetByRole(AriaRole.Combobox, new() { Name = "Kategori", Exact = true }).SelectOptionAsync(kategori);
        if (tentativ)
        {
            await Expect(Side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true })).ToBeVisibleAsync();
            await Side.GetByRole(AriaRole.Combobox, new() { Name = "Presisjon", Exact = true }).SelectOptionAsync("Maaned");
            await Side.GetByRole(AriaRole.Combobox, new() { Name = "Kvalifikator", Exact = true }).SelectOptionAsync(kvalifikator);
        }
        else if (tid is not null) await Side.GetByLabel("Klokkeslett", new() { Exact = false }).FillAsync(tid);
        await Side.GetByRole(AriaRole.Checkbox, new() { NameRegex = new($"\\({gruppe}\\)$") }).CheckAsync();
        await Side.GetByRole(AriaRole.Button, new() { Name = "Lagre", Exact = true }).ClickAsync();
        await Expect(Side.GetByRole(AriaRole.Heading, new() { Name = "Frister", Exact = true })).ToBeVisibleAsync();
    }

    private async Task RyddOpp()
        => await RyddOpp(_app, _playwright, _browser, _kontekster, Mappe, _applogg);

    private static async Task RyddOpp(Process app, IPlaywright? playwright, IBrowser? browser,
        IEnumerable<IBrowserContext> kontekster, string mappe, ConcurrentQueue<string> applogg)
    {
        var feil = new List<Exception>();
        async Task Forsok(Func<Task> handling)
        {
            try { await handling(); }
            catch (Exception e) { feil.Add(e); }
        }

        // Hver ressurs frigjøres selv om en tidligere lukking feiler; alle feil rapporteres.
        foreach (var ctx in kontekster) await Forsok(() => ctx.CloseAsync());
        if (browser is not null) await Forsok(() => browser.CloseAsync());
        await Forsok(() => { playwright?.Dispose(); return Task.CompletedTask; });
        await Forsok(async () =>
        {
            if (!app.HasExited) app.Kill(entireProcessTree: true);
            await app.WaitForExitAsync();
        });
        await Forsok(() => File.WriteAllLinesAsync(Path.Combine(mappe, "app.log"), applogg.Select(RedigerHemmelighet)));
        await Forsok(() => { app.Dispose(); return Task.CompletedTask; });
        if (feil.Count > 0) throw new AggregateException("Opprydding av nettlesertesten feilet.", feil);
    }

    public void BekreftIngenKretsfeil() => Assert.True(_feil.IsEmpty, string.Join("; ", _feil.Distinct()));

    private static string RedigerHemmelighet(string tekst)
        => System.Text.RegularExpressions.Regex.Replace(tekst, @"/kalender/feed/[^\s'""<>]+", "/kalender/feed/[skjult]");
}
