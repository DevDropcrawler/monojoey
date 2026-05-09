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
        var lockPlayer = gameState.Rules.Jail.Enabled;

        var players = gameState.Players.ToArray();
        players[playerIndex] = players[playerIndex] with
        {
            CurrentTileId = LockupTileId,
            IsLockedUp = lockPlayer,
            TurnState = players[playerIndex].TurnState with
            {
                JailTurnCount = 0,
                JailRollAttemptCount = 0,
                ConsecutiveDoublesCount = 0,
                LastJailReleaseReason = null,
            },
        };

        return gameState with { Players = players };
    }

    public static GameState GrantGetOutOfLockupEscape(
        GameState gameState,
        PlayerId playerId,
        CardId escapeId)
    {
        if (!gameState.Rules.Jail.EscapeCardsEnabled)
        {
            return gameState;
        }

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
        if (!gameState.Rules.Jail.EscapeCardsEnabled)
        {
            return new LockupEscapeUseResult(gameState, LockupEscapeUseResultKind.EscapeCardsDisabled);
        }

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

        var releasedGameState = ReleaseFromLockup(gameState, playerId, "held_escape");
        var releasedPlayers = releasedGameState.Players.ToArray();
        var releasedPlayer = releasedPlayers[playerIndex];
        var heldCardIds = releasedPlayer.HeldCardIds.ToHashSet();
        heldCardIds.Remove(escapeId);
        releasedPlayers[playerIndex] = releasedPlayer with { HeldCardIds = heldCardIds };

        return new LockupEscapeUseResult(
            releasedGameState with { Players = releasedPlayers },
            LockupEscapeUseResultKind.ClearedLockup);
    }

    public static GameState ReleaseFromLockup(
        GameState gameState,
        PlayerId playerId,
        string releaseReason)
    {
        if (string.IsNullOrWhiteSpace(releaseReason))
        {
            throw new ArgumentException("Lockup release reason must be non-empty.", nameof(releaseReason));
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        if (!player.IsLockedUp)
        {
            return gameState;
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            IsLockedUp = false,
            TurnState = player.TurnState with
            {
                JailTurnCount = 0,
                JailRollAttemptCount = 0,
                ConsecutiveDoublesCount = 0,
                LastJailReleaseReason = releaseReason,
            },
        };

        return gameState with { Players = players };
    }

    public static bool CanPayFineToExit(GameState gameState, PlayerId playerId)
    {
        var player = gameState.Players[FindPlayerIndex(gameState.Players, playerId)];

        return gameState.Rules.Jail.PayToExitEnabled &&
            player.IsLockedUp &&
            player.Money.Amount >= gameState.Rules.Jail.FineAmount;
    }

    public static LockupFinePaymentResult PayFineAndRelease(
        GameState gameState,
        PlayerId playerId,
        bool forcePayment = false)
    {
        var fineAmount = new Money(gameState.Rules.Jail.FineAmount);
        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];

        if (!player.IsLockedUp)
        {
            return new LockupFinePaymentResult(
                gameState,
                LockupFinePaymentResultKind.PlayerNotLockedUp,
                fineAmount);
        }

        if (!forcePayment && !gameState.Rules.Jail.PayToExitEnabled)
        {
            return new LockupFinePaymentResult(
                gameState,
                LockupFinePaymentResultKind.PayToExitDisabled,
                fineAmount);
        }

        if (player.Money.Amount < fineAmount.Amount)
        {
            return new LockupFinePaymentResult(
                gameState,
                LockupFinePaymentResultKind.InsufficientCash,
                fineAmount);
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with { Money = new Money(player.Money.Amount - fineAmount.Amount) };
        var paidGameState = gameState with { Players = players };

        return new LockupFinePaymentResult(
            ReleaseFromLockup(paidGameState, playerId, "paid_fine"),
            LockupFinePaymentResultKind.PaidAndReleased,
            fineAmount);
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
