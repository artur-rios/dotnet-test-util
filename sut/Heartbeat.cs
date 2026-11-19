namespace ArturRios.Util.Test.Sut;

/// <summary>
/// A service holding a timer that is started with the host and never stopped, as ASP.NET Core's rate limiter holds
/// one. The timer captures the execution context it is started in: started in a test's, it keeps whatever that
/// context reaches alive for the rest of the process, which is what the host-leak tests look for.
/// </summary>
public sealed class Heartbeat
{
    // Deliberately never disposed. Its callback and state reference nothing, so only the captured context can.
    private readonly Timer _timer = new(static _ => { }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
}
