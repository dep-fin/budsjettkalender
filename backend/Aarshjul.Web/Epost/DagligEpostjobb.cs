using Aarshjul.Application.Epost;

namespace Aarshjul.Web.Epost;

/// <summary>Startes etter Webs skjemaoppstart. KjorAsync eier lokal dato og varig konkurransevern.</summary>
public sealed class DagligEpostjobb(IServiceScopeFactory scopefabrikk, TimeProvider klokke,
    ILogger<DagligEpostjobb> logg) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), klokke);
        do
        {
            try
            {
                using var scope = scopefabrikk.CreateScope();
                var resultat = await scope.ServiceProvider.GetRequiredService<IDagligEpostoppsummering>().KjorAsync(stoppingToken);
                foreach (var gruppe in resultat.Mottakere.GroupBy(x => x.Status))
                    logg.LogInformation("Daglig e-post: {Status}, antall {Antall}.", gruppe.Key, gruppe.Count());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception) { logg.LogWarning("Daglig e-post kunne ikke fullføres. Varig status beholdes; se driftsveiledningen."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
