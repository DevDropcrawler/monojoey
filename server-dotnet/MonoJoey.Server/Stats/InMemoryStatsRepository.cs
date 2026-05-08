namespace MonoJoey.Server.Stats;

using MonoJoey.Server.GameEngine.Stats;

internal sealed class InMemoryStatsRepository : IStatEventSink
{
    private readonly object sync = new();
    private readonly Dictionary<string, MutablePlayerStats> playerStats = new(StringComparer.Ordinal);

    public void Emit(StatEvent evt)
    {
        try
        {
            Record(evt);
        }
        catch
        {
        }
    }

    public PlayerStatsSnapshot? GetPlayerStats(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return null;
        }

        lock (sync)
        {
            return playerStats.TryGetValue(playerId, out var stats)
                ? stats.ToSnapshot()
                : null;
        }
    }

    public IReadOnlyList<PlayerStatsSnapshot> GetAllPlayerStats()
    {
        lock (sync)
        {
            return playerStats.Values
                .Select(stats => stats.ToSnapshot())
                .ToArray();
        }
    }

    private void Record(StatEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.PlayerId.Value))
        {
            return;
        }

        var playerId = evt.PlayerId.Value;
        var amount = evt.Amount?.Amount;
        if (amount < 0)
        {
            return;
        }

        if (!IsTrackedKind(evt.Kind))
        {
            return;
        }

        lock (sync)
        {
            var stats = GetOrCreateStats(playerId);
            switch (evt.Kind)
            {
                case StatEventKind.GameWon:
                    stats.GamesWon++;
                    break;
                case StatEventKind.RentPaid:
                    stats.RentPaid += amount.GetValueOrDefault();
                    break;
                case StatEventKind.RentReceived:
                    stats.RentCollected += amount.GetValueOrDefault();
                    break;
                case StatEventKind.AuctionWon:
                    stats.AuctionsWon++;
                    break;
                case StatEventKind.LoanTaken:
                    stats.LoansTaken++;
                    break;
                case StatEventKind.CardTriggered:
                    stats.CardsTriggered++;
                    break;
                case StatEventKind.SlimerApplied:
                    stats.SlimerApplied++;
                    break;
                case StatEventKind.EarthquakeApplied:
                    stats.EarthquakePropertiesDamaged += amount ?? 1;
                    break;
                case StatEventKind.PropertyRepaired:
                    stats.PropertyRepairSpend += amount.GetValueOrDefault();
                    break;
            }
        }
    }

    private static bool IsTrackedKind(StatEventKind kind)
    {
        return kind is
            StatEventKind.GameWon or
            StatEventKind.RentPaid or
            StatEventKind.RentReceived or
            StatEventKind.AuctionWon or
            StatEventKind.LoanTaken or
            StatEventKind.CardTriggered or
            StatEventKind.SlimerApplied or
            StatEventKind.EarthquakeApplied or
            StatEventKind.PropertyRepaired;
    }

    private MutablePlayerStats GetOrCreateStats(string playerId)
    {
        if (playerStats.TryGetValue(playerId, out var stats))
        {
            return stats;
        }

        stats = new MutablePlayerStats(playerId);
        playerStats.Add(playerId, stats);
        return stats;
    }

    private sealed class MutablePlayerStats
    {
        public MutablePlayerStats(string playerId)
        {
            PlayerId = playerId;
        }

        public string PlayerId { get; }

        public long GamesWon { get; set; }

        public long RentPaid { get; set; }

        public long RentCollected { get; set; }

        public long AuctionsWon { get; set; }

        public long LoansTaken { get; set; }

        public long CardsTriggered { get; set; }

        public long SlimerApplied { get; set; }

        public long EarthquakePropertiesDamaged { get; set; }

        public long PropertyRepairSpend { get; set; }

        public PlayerStatsSnapshot ToSnapshot()
        {
            return new PlayerStatsSnapshot(
                PlayerId,
                GamesWon,
                RentPaid,
                RentCollected,
                AuctionsWon,
                LoansTaken,
                CardsTriggered,
                SlimerApplied,
                EarthquakePropertiesDamaged,
                PropertyRepairSpend);
        }
    }
}
