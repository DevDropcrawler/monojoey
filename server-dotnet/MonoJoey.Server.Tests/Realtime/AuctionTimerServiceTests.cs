namespace MonoJoey.Server.Tests.Realtime;

using MonoJoey.Server.Realtime;

public class AuctionTimerServiceTests
{
    [Fact]
    public void Schedule_ReplacesExistingTimerForSession()
    {
        using var service = new AuctionTimerService();
        var firstDeadline = DateTimeOffset.UtcNow.AddMinutes(10);
        var secondDeadline = firstDeadline.AddSeconds(3);

        service.Schedule("session_1", firstDeadline);
        var foundFirstTimer = service.TryGetActiveTimerForTesting(
            "session_1",
            out _,
            out var firstVersion);
        service.Schedule("session_1", secondDeadline);
        var foundSecondTimer = service.TryGetActiveTimerForTesting(
            "session_1",
            out var activeDeadline,
            out var secondVersion);

        Assert.True(foundFirstTimer);
        Assert.True(foundSecondTimer);
        Assert.Equal(1, service.ActiveTimerCount);
        Assert.Equal(secondDeadline, activeDeadline);
        Assert.NotEqual(firstVersion, secondVersion);
    }

    [Fact]
    public void Cancel_RemovesActiveTimerForSession()
    {
        using var service = new AuctionTimerService();
        service.Schedule("session_1", DateTimeOffset.UtcNow.AddMinutes(10));

        service.Cancel("session_1");

        Assert.Equal(0, service.ActiveTimerCount);
    }

    [Fact]
    public async Task TriggerExpiredTimer_ReplacedVersionDoesNotInvokeHandler()
    {
        using var service = new AuctionTimerService();
        var callbackCount = 0;
        service.SetExpiryHandler((_, _, _) =>
        {
            callbackCount++;
            return Task.CompletedTask;
        });
        var firstDeadline = DateTimeOffset.UtcNow.AddMinutes(10);
        var secondDeadline = firstDeadline.AddSeconds(3);
        service.Schedule("session_1", firstDeadline);
        _ = service.TryGetActiveTimerForTesting("session_1", out _, out var firstVersion);
        service.Schedule("session_1", secondDeadline);

        await service.TriggerExpiredTimerForTestingAsync("session_1", firstDeadline, firstVersion);

        Assert.Equal(0, callbackCount);
        Assert.Equal(1, service.ActiveTimerCount);
    }

    [Fact]
    public async Task TriggerExpiredTimer_ActiveVersionInvokesHandler()
    {
        using var service = new AuctionTimerService();
        var callbackCount = 0;
        DateTimeOffset? capturedDeadline = null;
        service.SetExpiryHandler((_, deadline, _) =>
        {
            callbackCount++;
            capturedDeadline = deadline;
            return Task.CompletedTask;
        });
        var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
        service.Schedule("session_1", deadline);
        _ = service.TryGetActiveTimerForTesting("session_1", out _, out var version);

        await service.TriggerExpiredTimerForTestingAsync("session_1", deadline, version);

        Assert.Equal(1, callbackCount);
        Assert.Equal(deadline, capturedDeadline);
    }
}
