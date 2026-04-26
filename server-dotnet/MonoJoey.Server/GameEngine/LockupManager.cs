namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class LockupManager
{
    private static readonly TileId LockupTileId = new("lockup_01");

    public static GameState SendToLockup(GameState gameState, PlayerId playerId)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        _ = FindLockupTile(gameState.Board);

        var players = gameState.Players.ToArray();
        players[playerIndex] = players[playerIndex] with
        {
            CurrentTileId = LockupTileId,
            IsLockedUp = true,
        };

        return gameState with { Players = players };
    }

    public static GameState GrantGetOutOfLockupEscape(
        GameState gameState,
        PlayerId playerId,
        CardId escapeId)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];

        if (player.HeldCardIds.Contains(escapeId))
        {
            return gameState;
        }

        var heldCardIds = player.HeldCardIds.ToHashSet();
        heldCardIds.Add(escapeId);

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with { HeldCardIds = heldCardIds };

        return gameState with { Players = players };
    }

    public static LockupEscapeUseResult UseGetOutOfLockupEscape(
        GameState gameState,
        PlayerId playerId,
        CardId escapeId)
    {
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];

        if (!player.IsLockedUp)
        {
            return new LockupEscapeUseResult(gameState, LockupEscapeUseResultKind.PlayerNotLockedUp);
        }

        if (!player.HeldCardIds.Contains(escapeId))
        {
            return new LockupEscapeUseResult(gameState, LockupEscapeUseResultKind.EscapeNotHeld);
        }

        var heldCardIds = player.HeldCardIds.ToHashSet();
        heldCardIds.Remove(escapeId);

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            HeldCardIds = heldCardIds,
            IsLockedUp = false,
        };

        return new LockupEscapeUseResult(
            gameState with { Players = players },
            LockupEscapeUseResultKind.ClearedLockup);
    }

    private static int FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Lockup player must exist in the game player list.");
    }

    private static Tile FindLockupTile(Board board)
    {
        foreach (var tile in board.Tiles)
        {
            if (tile.TileId == LockupTileId && tile.TileType == TileType.Lockup)
            {
                return tile;
            }
        }

        throw new InvalidOperationException("A board must define a lockup tile before players can be locked up.");
    }
}
