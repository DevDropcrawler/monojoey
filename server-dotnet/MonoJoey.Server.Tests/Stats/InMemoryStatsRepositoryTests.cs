namespace MonoJoey.Server.Tests.Stats;

using MonoJoey.Server.GameEngine;
using MonoJoey.Server.GameEngine.Stats;
using MonoJoey.Server.Stats;
using MonoJoey.Shared.Protocol;

public class InMemoryStatsRepositoryTests
{
    [Fact]
    public void Emit_AggregatesEachTrackedEventIntoExpectedCounter()
    {
        var repository = new InMemoryStatsRepository();
        var playerId = new PlayerId("player_1");

        repository.Emit(new StatEvent(playerId, StatEventKind.GameWon));
        repository.Emit(new StatEvent(playerId, StatEventKind.RentPaid, new Money(40)));
        repository.Emit(new StatEvent(playerId, StatEventKind.RentReceived, new Money(25)));
        repository.Emit(new StatEvent(playerId, StatEventKind.AuctionWon, new Money(300)));
        repository.Emit(new StatEvent(playerId, StatEventKind.LoanTaken, new Money(100)));
        repository.Emit(new StatEvent(playerId, StatEventKind.CardTriggered));
        repository.Emit(new StatEvent(playerId, StatEventKind.SlimerApplied));
        repository.Emit(new StatEvent(playerId, StatEventKind.EarthquakeApplied));
        repository.Emit(new StatEvent(playerId, StatEventKind.PropertyRepaired, new Money(75)));

        var stats = repository.GetPlayerStats("player_1");

        Assert.NotNull(stats);
        Assert.Equal("player_1", stats.PlayerId);
        Assert.Equal(1, stats.GamesWon);
        Assert.Equal(40, stats.RentPaid);
        Assert.Equal(25, stats.RentCollected);
        Assert.Equal(1, stats.AuctionsWon);
        Assert.Equal(1, stats.LoansTaken);
        Assert.Equal(1, stats.CardsTriggered);
        Assert.Equal(1, stats.SlimerApplied);
        Assert.Equal(1, stats.EarthquakePropertiesDamaged);
        Assert.Equal(75, stats.PropertyRepairSpend);
    }

    [Fact]
    public void Emit_AccumulatesMultipleEventsForSamePlayer()
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.RentPaid, new Money(10)));
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.RentPaid, new Money(15)));
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));

        var stats = repository.GetPlayerStats("player_1");

        Assert.NotNull(stats);
        Assert.Equal(25, stats.RentPaid);
        Assert.Equal(2, stats.GamesWon);
    }

    [Fact]
    public void Emit_KeepsSeparatePlayersIsolated()
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.RentPaid, new Money(10)));
        repository.Emit(new StatEvent(new PlayerId("player_2"), StatEventKind.RentPaid, new Money(20)));

        Assert.Equal(10, repository.GetPlayerStats("player_1")?.RentPaid);
        Assert.Equal(20, repository.GetPlayerStats("player_2")?.RentPaid);
    }

    [Fact]
    public void Emit_IgnoresUnknownEvents()
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId("player_1"), (StatEventKind)999));

        Assert.Null(repository.GetPlayerStats("player_1"));
        Assert.Empty(repository.GetAllPlayerStats());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Emit_IgnoresMissingPlayerIds(string playerId)
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId(playerId), StatEventKind.GameWon));

        Assert.Empty(repository.GetAllPlayerStats());
    }

    [Fact]
    public void Emit_IgnoresNegativeAmounts()
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.RentPaid, new Money(-1)));
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.EarthquakeApplied, new Money(-2)));

        Assert.Empty(repository.GetAllPlayerStats());
    }

    [Fact]
    public void Emit_SwallowsAggregationExceptionsInternally()
    {
        var repository = new InMemoryStatsRepository();

        var exception = Record.Exception(() => repository.Emit(null!));

        Assert.Null(exception);
        Assert.Empty(repository.GetAllPlayerStats());
    }

    [Fact]
    public void Reads_ReturnDefensiveSnapshots()
    {
        var repository = new InMemoryStatsRepository();

        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));
        var snapshot = repository.GetPlayerStats("player_1");
        var allSnapshots = repository.GetAllPlayerStats();

        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));
        repository.Emit(new StatEvent(new PlayerId("player_2"), StatEventKind.GameWon));

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.GamesWon);
        Assert.Single(allSnapshots);
        Assert.Equal(2, repository.GetPlayerStats("player_1")?.GamesWon);
        Assert.Equal(2, repository.GetAllPlayerStats().Count);
    }
}
