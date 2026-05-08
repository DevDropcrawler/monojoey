namespace MonoJoey.Server.Stats;

internal sealed record PlayerStatsSnapshot(
    string PlayerId,
    long GamesWon,
    long RentPaid,
    long RentCollected,
    long AuctionsWon,
    long LoansTaken,
    long CardsTriggered,
    long SlimerApplied,
    long EarthquakePropertiesDamaged,
    long PropertyRepairSpend);
