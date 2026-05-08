namespace MonoJoey.Server.Stats;

internal sealed class LeaderboardQueryService
{
    public const int DefaultLimit = 10;
    public const int MinimumLimit = 1;
    public const int MaximumLimit = 100;

    private readonly InMemoryStatsRepository repository;

    public LeaderboardQueryService(InMemoryStatsRepository repository)
    {
        this.repository = repository;
    }

    public IReadOnlyList<LeaderboardEntry> GetLeaderboard(
        LeaderboardCategory category,
        int limit = DefaultLimit)
    {
        var normalizedLimit = NormalizeLimit(limit);
        return repository.GetAllPlayerStats()
            .Select(snapshot => new
            {
                snapshot.PlayerId,
                Value = GetCategoryValue(snapshot, category),
            })
            .Where(entry => entry.Value > 0)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.PlayerId, StringComparer.Ordinal)
            .Take(normalizedLimit)
            .Select((entry, index) => new LeaderboardEntry(index + 1, entry.PlayerId, entry.Value))
            .ToArray();
    }

    public PlayerStatsSnapshot? GetPlayerStats(string playerId)
    {
        return repository.GetPlayerStats(playerId);
    }

    public static int NormalizeLimit(int limit)
    {
        return Math.Clamp(limit, MinimumLimit, MaximumLimit);
    }

    private static long GetCategoryValue(PlayerStatsSnapshot snapshot, LeaderboardCategory category)
    {
        return category switch
        {
            LeaderboardCategory.Wins => snapshot.GamesWon,
            LeaderboardCategory.RentPaid => snapshot.RentPaid,
            LeaderboardCategory.RentCollected => snapshot.RentCollected,
            LeaderboardCategory.AuctionsWon => snapshot.AuctionsWon,
            LeaderboardCategory.LoansTaken => snapshot.LoansTaken,
            LeaderboardCategory.CardsTriggered => snapshot.CardsTriggered,
            _ => 0,
        };
    }
}
