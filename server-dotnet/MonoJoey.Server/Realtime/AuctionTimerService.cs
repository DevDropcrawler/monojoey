namespace MonoJoey.Server.Realtime;

public sealed class AuctionTimerService : IDisposable
{
    private readonly object timerLock = new();
    private readonly Dictionary<string, ActiveAuctionTimer> activeTimers = new(StringComparer.Ordinal);
    private Func<string, DateTimeOffset, CancellationToken, Task>? expiryHandler;
    private long nextVersion;
    private bool disposed;

    public void SetExpiryHandler(Func<string, DateTimeOffset, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (timerLock)
        {
            ThrowIfDisposed();
            expiryHandler = handler;
        }
    }

    public void Schedule(string sessionId, DateTimeOffset timerEndsAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        Timer? previousTimer = null;
        lock (timerLock)
        {
            ThrowIfDisposed();
            if (activeTimers.TryGetValue(sessionId, out var previous))
            {
                previousTimer = previous.Timer;
            }

            var version = ++nextVersion;
            var timer = new Timer(
                _ => _ = HandleTimerElapsedAsync(sessionId, timerEndsAtUtc, version, CancellationToken.None),
                state: null,
                dueTime: Timeout.InfiniteTimeSpan,
                period: Timeout.InfiniteTimeSpan);

            activeTimers[sessionId] = new ActiveAuctionTimer(timerEndsAtUtc, version, timer);
            timer.Change(GetDueTime(timerEndsAtUtc), Timeout.InfiniteTimeSpan);
        }

        previousTimer?.Dispose();
    }

    public void Cancel(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        Timer? timer = null;
        lock (timerLock)
        {
            if (activeTimers.Remove(sessionId, out var activeTimer))
            {
                timer = activeTimer.Timer;
            }
        }

        timer?.Dispose();
    }

    public void Dispose()
    {
        Timer[] timers;
        lock (timerLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            timers = activeTimers.Values.Select(activeTimer => activeTimer.Timer).ToArray();
            activeTimers.Clear();
        }

        foreach (var timer in timers)
        {
            timer.Dispose();
        }
    }

    internal int ActiveTimerCount
    {
        get
        {
            lock (timerLock)
            {
                return activeTimers.Count;
            }
        }
    }

    internal bool TryGetActiveTimerForTesting(
        string sessionId,
        out DateTimeOffset timerEndsAtUtc,
        out long version)
    {
        lock (timerLock)
        {
            if (activeTimers.TryGetValue(sessionId, out var activeTimer))
            {
                timerEndsAtUtc = activeTimer.TimerEndsAtUtc;
                version = activeTimer.Version;
                return true;
            }
        }

        timerEndsAtUtc = default;
        version = default;
        return false;
    }

    internal Task TriggerExpiredTimerForTestingAsync(
        string sessionId,
        DateTimeOffset timerEndsAtUtc,
        long version,
        CancellationToken cancellationToken = default)
    {
        return HandleTimerElapsedAsync(sessionId, timerEndsAtUtc, version, cancellationToken);
    }

    private async Task HandleTimerElapsedAsync(
        string sessionId,
        DateTimeOffset timerEndsAtUtc,
        long version,
        CancellationToken cancellationToken)
    {
        Func<string, DateTimeOffset, CancellationToken, Task>? handler;
        lock (timerLock)
        {
            if (!activeTimers.TryGetValue(sessionId, out var activeTimer) ||
                activeTimer.Version != version ||
                activeTimer.TimerEndsAtUtc != timerEndsAtUtc)
            {
                return;
            }

            handler = expiryHandler;
        }

        if (handler is null)
        {
            Cancel(sessionId);
            return;
        }

        try
        {
            await handler(sessionId, timerEndsAtUtc, cancellationToken);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private static TimeSpan GetDueTime(DateTimeOffset timerEndsAtUtc)
    {
        var dueTime = timerEndsAtUtc - DateTimeOffset.UtcNow;
        return dueTime <= TimeSpan.Zero ? TimeSpan.Zero : dueTime;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record ActiveAuctionTimer(
        DateTimeOffset TimerEndsAtUtc,
        long Version,
        Timer Timer);
}
