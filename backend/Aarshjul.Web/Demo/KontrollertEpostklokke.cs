namespace Aarshjul.Web.Demo;

/// <summary>Kun eksplisitt kontrollert demo/Testing. Automatiske jobber bruker aldri denne klokken.</summary>
public sealed class KontrollertEpostklokke : TimeProvider
{
    private long _ticks = new DateTimeOffset(2030, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
    public void Sett(DateTimeOffset naa) => Interlocked.Exchange(ref _ticks, naa.UtcTicks);
}
