namespace MonoJoey.Server.Tests.Stats;

using MonoJoey.Server.GameEngine;
using MonoJoey.Server.GameEngine.Stats;
using MonoJoey.Server.Stats;
using MonoJoey.Shared.Protocol;

public class LeaderboardQueryServiceTests
{
    [Fact]
    public void GetLeaderboard_SortsBySelectedValueDescending()
    {
        var repository = new InMemoryStatsRepository();
        repository.Emit(new StatEvent(new PlayerId("player_low"), StatEventKind.RentPaid, new Money(10)));
        repository.Emit(new StatEvent(new PlayerId("player_high"), StatEventKind.RentPaid, new Money(30)));
        var service = new LeaderboardQueryService(repository);

        var entries = service.GetLeaderboard(LeaderboardCategory.RentPaid, 10);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(1, entry.Rank);
                Assert.Equal("player_high", entry.PlayerId);
                Assert.Equal(30, entry.Value);
            },
            entry =>
            {
                Assert.Equal(2, entry.Rank);
                Assert.Equal("player_low", entry.PlayerId);
                Assert.Equal(10, entry.Value);
            });
    }

    [Fact]
    public void GetLeaderboard_UsesPlayerIdAscendingOrdinalTieBreaker()
    {
        var repository = new InMemoryStatsRepository();
        repository.Emit(new StatEvent(new PlayerId("player_b"), StatEventKind.GameWon));
        repository.Emit(new StatEvent(new PlayerId("player_a"), StatEventKind.GameWon));
        var service = new LeaderboardQueryService(repository);

        var entries = service.GetLeaderboard(LeaderboardCategory.Wins, 10);

        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(1, entry.Rank);
                Assert.Equal("player_a", entry.PlayerId);
            },
            entry =>
            {
                Assert.Equal(2, entry.Rank);
                Assert.Equal("player_b", entry.PlayerId);
            });
    }

    [Fact]
    public void GetLeaderboard_ExcludesZeroValuePlayers()
    {
        var repository = new InMemoryStatsRepository();
        repository.Emit(new StatEvent(new PlayerId("player_zero"), StatEventKind.RentPaid, new Money(0)));
        repository.Emit(new StatEvent(new PlayerId("player_one"), StatEventKind.GameWon));
        var service = new LeaderboardQueryService(repository);

        var entries = service.GetLeaderboard(LeaderboardCategory.RentPaid, 10);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetLeaderboard_AppliesMinimumAndMaximumLimits()
    {
        var repository = new InMemoryStatsRepository();
        for (var i = 0; i < 105; i++)
        {
            repository.Emit(new StatEvent(new PlayerId($"player_{i:000}"), StatEventKind.GameWon));
        }

        var service = new LeaderboardQueryService(repository);

        Assert.Single(service.GetLeaderboard(LeaderboardCategory.Wins, 0));
        Assert.Equal(100, service.GetLeaderboard(LeaderboardCategory.Wins, 500).Count);
    }

    [Fact]
    public void GetLeaderboard_UsesDefaultLimit()
    {
        var repository = new InMemoryStatsRepository();
        for (var i = 0; i < 12; i++)
        {
            repository.Emit(new StatEvent(new PlayerId($"player_{i:000}"), StatEventKind.GameWon));
        }

        var service = new LeaderboardQueryService(repository);

        Assert.Equal(10, service.GetLeaderboard(LeaderboardCategory.Wins).Count);
    }

    [Theory]
    [InlineData("wins", nameof(StatEventKind.GameWon), null, 1)]
    [InlineData("rent_paid", nameof(StatEventKind.RentPaid), 15, 15)]
    [InlineData("rent_collected", nameof(StatEventKind.RentReceived), 20, 20)]
    [InlineData("auctions_won", nameof(StatEventKind.AuctionWon), null, 1)]
    [InlineData("loans_taken", nameof(StatEventKind.LoanTaken), null, 1)]
    [InlineData("cards_triggered", nameof(StatEventKind.CardTriggered), null, 1)]
    public void GetLeaderboard_SupportsAllRequiredCategories(
        string categoryName,
        string eventKindName,
        int? amount,
        long expectedValue)
    {
        var repository = new InMemoryStatsRepository();
        var eventKind = Enum.Parse<StatEventKind>(eventKindName);
        repository.Emit(new StatEvent(
            new PlayerId("player_1"),
            eventKind,
            amount is null ? null : new Money(amount.Value)));
        var service = new LeaderboardQueryService(repository);

        var parsed = LeaderboardCategoryNames.TryParse(categoryName, out var category);

        Assert.True(parsed);
        var entry = Assert.Single(service.GetLeaderboard(category, 10));

        Assert.Equal(1, entry.Rank);
        Assert.Equal("player_1", entry.PlayerId);
        Assert.Equal(expectedValue, entry.Value);
    }
}
