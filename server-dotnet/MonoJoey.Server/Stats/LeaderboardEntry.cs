namespace MonoJoey.Server.Stats;

internal sealed record LeaderboardEntry(
    int Rank,
    string PlayerId,
    long Value);
