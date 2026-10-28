using Aarshjul.Application.Epost;
using Aarshjul.Web.Epost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aarshjul.Tests;

public sealed class DagligEpostjobbTester
{
    [Fact]
    public async Task Vertens_jobb_kjorer_ved_start_og_timeproviderens_neste_vekking_og_stanser()
    {
        var teller = new Kjoringer(); var klokke = new Timerklokke();
        await using var tjenester = new ServiceCollection().AddScoped<IDagligEpostoppsummering>(_ => teller).BuildServiceProvider();
        using var jobb = new DagligEpostjobb(tjenester.GetRequiredService<IServiceScopeFactory>(), klokke, NullLogger<DagligEpostjobb>.Instance);
        await jobb.StartAsync(default);
        await teller.Foerste.Task.WaitAsync(TimeSpan.FromSeconds(15));
        klokke.Vekk();
        await teller.Andre.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await jobb.StopAsync(default);
        klokke.Vekk();
        Assert.Equal(2, teller.Antall);
    }

    [Fact]
    public void Testing_starter_ingen_databasespoerring_foer_TestApplikasjon_har_opprettet_skjema()
    {
        using var app = new TestApplikasjon();
        Assert.DoesNotContain(app.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), x => x is DagligEpostjobb);
    }

    private sealed class Kjoringer : IDagligEpostoppsummering
    {
        public int Antall;
        public readonly TaskCompletionSource Foerste = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Andre = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Oppsummeringsresultat> KjorAsync(CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref Antall);
            if (n == 1) Foerste.TrySetResult(); if (n == 2) Andre.TrySetResult();
            return Task.FromResult(new Oppsummeringsresultat(new(2030, 1, 1), false, []));
        }
    }
    private sealed class Timerklokke : TimeProvider
    {
        private Testtimer? _timer;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => _timer = new(callback, state);
        public void Vekk() => _timer!.Vekk();
        private sealed class Testtimer(TimerCallback callback, object? state) : ITimer
        {
            private bool _lukket;
            public void Vekk() { if (!_lukket) callback(state); }
            public bool Change(TimeSpan dueTime, TimeSpan period) => !_lukket;
            public void Dispose() => _lukket = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
