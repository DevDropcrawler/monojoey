namespace MonoJoey.Shared.Protocol;

public readonly record struct MessageId(string Value);

public readonly record struct EventId(string Value);

public readonly record struct LobbyId(string Value);

public readonly record struct MatchId(string Value);

public readonly record struct PlayerId(string Value);

public readonly record struct TileId(string Value);

public readonly record struct CardId(string Value);

public readonly record struct AuctionId(string Value);

public readonly record struct RulesetId(string Value);

public readonly record struct BoardId(string Value);
