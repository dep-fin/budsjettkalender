using System.Data.Common;
using Aarshjul.Application.Frister;
using Aarshjul.Application.Synlighet;
using Aarshjul.Domain;
using Aarshjul.Infrastructure;
using Aarshjul.Infrastructure.Frister;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Aarshjul.Tests;

public class OperasjonskontekstTester
{
    [Fact]
    public async Task Samme_lesetjeneste_kan_fullfoere_et_kall_mens_et_annet_venter_paa_databasen()
    {
        var tilkobling = $"Data Source=operasjoner-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        await using var holdepunkt = new SqliteConnection(tilkobling);
        await holdepunkt.OpenAsync();
        var port = new FoersteLesingPort();
        var tjenester = new ServiceCollection();
        tjenester.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(tilkobling).AddInterceptors(port));
        tjenester.AddSingleton<TimeProvider>(new FastKlokke(new DateOnly(2027, 1, 1)));
        tjenester.AddScoped<IFristlesing, FristTjeneste>();
        await using var tilbyder = tjenester.BuildServiceProvider();
        await using (var db = await tilbyder.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Frister.Add(new Frist
            {
                Id = Guid.NewGuid(), Tittel = "Synlig frist", Dato = new DateOnly(2027, 6, 1),
                Budsjettaar = 2028, Status = FristStatus.Godkjent
            });
            await db.SaveChangesAsync();
        }

        await using var scope = tilbyder.CreateAsyncScope();
        var lesing = scope.ServiceProvider.GetRequiredService<IFristlesing>();
        Assert.Same(lesing, scope.ServiceProvider.GetRequiredService<IFristlesing>());
        port.Aktiver();
        var foerste = lesing.HentSynligeAsync(new Administrator(), new FristFilter());
        try
        {
            await port.Venter.WaitAsync(TimeSpan.FromSeconds(10));
            var andre = await lesing.HentSynligeAsync(new Administrator(), new FristFilter())
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("Synlig frist", Assert.Single(andre).Tittel);
        }
        finally
        {
            port.Slipp();
            await foerste.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task Samme_skrivetjeneste_kan_lagre_en_frist_mens_et_annet_kall_venter_paa_databasen()
    {
        var tilkobling = $"Data Source=skriving-{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        await using var holdepunkt = new SqliteConnection(tilkobling);
        await holdepunkt.OpenAsync();
        var port = new FoersteLesingPort();
        var tjenester = new ServiceCollection();
        tjenester.AddDbContextFactory<AppDbContext>(o => o.UseSqlite(tilkobling).AddInterceptors(port));
        tjenester.AddScoped<IFristskriving, FristskrivingTjeneste>();
        await using var tilbyder = tjenester.BuildServiceProvider();
        await using (var db = await tilbyder.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Synlighetsgrupper.Add(new Synlighetsgruppe { Id = Guid.NewGuid(), Kode = "FA", Navn = "FA" });
            await db.SaveChangesAsync();
        }

        await using var scope = tilbyder.CreateAsyncScope();
        var skriving = scope.ServiceProvider.GetRequiredService<IFristskriving>();
        Assert.Same(skriving, scope.ServiceProvider.GetRequiredService<IFristskriving>());
        port.Aktiver();
        var foerste = skriving.OpprettAsync(Inndata("Første"));
        Guid andreId;
        try
        {
            await port.Venter.WaitAsync(TimeSpan.FromSeconds(10));
            andreId = await skriving.OpprettAsync(Inndata("Andre")).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("Andre", (await skriving.HentForRedigeringAsync(andreId))!.Tittel);
        }
        finally
        {
            port.Slipp();
            await foerste.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal("Første", (await skriving.HentForRedigeringAsync(await foerste))!.Tittel);
        Assert.Equal("Andre", (await skriving.HentForRedigeringAsync(andreId))!.Tittel);
    }

    private static FristInndata Inndata(string tittel) => new()
    {
        Tittel = tittel, Dato = new DateOnly(2027, 6, 1), Budsjettaar = 2028, Synlighetskoder = ["FA"]
    };

    private sealed class Administrator : ISynlighetskontekst
    {
        public bool SerAlt => true;
        public IReadOnlyCollection<string> Grupper { get; } = Array.Empty<string>();
    }

    private sealed class FoersteLesingPort : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _venter = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _slipp = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _aktiv;
        public Task Venter => _venter.Task;
        public void Aktiver() => _aktiv = 1;
        public void Slipp() => _slipp.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && Interlocked.CompareExchange(ref _aktiv, 0, 1) == 1)
            {
                _venter.TrySetResult();
                await _slipp.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
}
