namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class TurnManager
{
    public static GameState StartFirstTurn(GameState gameState)
    {
        if (gameState.Players.Count == 0)
        {
            throw new InvalidOperationException("A game must have at least one player before turns can start.");
        }

        var firstActivePlayer = SelectFirstTurnPlayer(gameState.Players, gameState.Rules.Jail.Enabled);
        if (firstActivePlayer is null)
        {
            throw new InvalidOperationException("A game must have at least one active player before turns can start.");
        }

        var startedGameState = gameState with
        {
            CurrentTurnPlayerId = firstActivePlayer.PlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = 1,
            HasRolledThisTurn = false,
            HasResolvedTileThisTurn = false,
            HasExecutedTileThisTurn = false,
            SuppressDoublesExtraTurnThisTurn = false,
            ActiveAuctionState = null,
        };

        return ApplyStartTurnEffects(startedGameState, firstActivePlayer.PlayerId);
    }

    public static Player GetCurrentPlayer(GameState gameState)
    {
        if (gameState.CurrentTurnPlayerId is null)
        {
            throw new InvalidOperationException("No current turn player is set.");
        }

        var currentPlayer = gameState.Players.Single(player => player.PlayerId == gameState.CurrentTurnPlayerId.Value);
        if (currentPlayer.IsEliminated)
        {
            throw new InvalidOperationException("Eliminated players cannot take turns.");
        }

        return currentPlayer;
    }

    public static GameState AdvanceToNextTurn(GameState gameState)
    {
        if (gameState.Players.Count == 0)
        {
            throw new InvalidOperationException("A game must have at least one player before turns can advance.");
        }

        if (gameState.CurrentTurnPlayerId is null)
        {
            return StartFirstTurn(gameState);
        }

        var currentTurnPlayerId = gameState.CurrentTurnPlayerId.Value;
        var resetGameState = PlayerTurnStateManager.ResetConsecutiveDoubles(gameState, currentTurnPlayerId);
        var currentIndex = FindCurrentPlayerIndex(resetGameState.Players, currentTurnPlayerId);
        var nextIndex = FindNextActivePlayerIndex(
            resetGameState.Players,
            currentIndex,
            resetGameState.Rules.Jail.Enabled);

        var nextPlayerId = resetGameState.Players[nextIndex].PlayerId;
        var nextGameState = resetGameState with
        {
            CurrentTurnPlayerId = nextPlayerId,
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = resetGameState.TurnNumber + 1,
            HasRolledThisTurn = false,
            HasResolvedTileThisTurn = false,
            HasExecutedTileThisTurn = false,
            SuppressDoublesExtraTurnThisTurn = false,
            ActiveAuctionState = null,
        };

        return ApplyStartTurnEffectsAndSkipEliminatedNonTerminalPlayers(nextGameState, nextPlayerId);
    }

    public static GameState AdvanceToExtraTurn(GameState gameState)
    {
        if (gameState.CurrentTurnPlayerId is null)
        {
            throw new InvalidOperationException("No current turn player is set.");
        }

        var currentPlayer = gameState.Players.Single(player => player.PlayerId == gameState.CurrentTurnPlayerId.Value);
        if (currentPlayer.IsEliminated)
        {
            throw new InvalidOperationException("Eliminated players cannot receive extra turns.");
        }

        if (currentPlayer.IsLockedUp)
        {
            throw new InvalidOperationException("Locked players cannot receive extra turns.");
        }

        return gameState with
        {
            Phase = GamePhase.AwaitingRoll,
            TurnNumber = gameState.TurnNumber + 1,
            HasRolledThisTurn = false,
            HasResolvedTileThisTurn = false,
            HasExecutedTileThisTurn = false,
            SuppressDoublesExtraTurnThisTurn = false,
            ActiveAuctionState = null,
        };
    }

    private static GameState ApplyStartTurnEffects(GameState gameState, PlayerId playerId)
    {
        var afterLoanInterest = LoanManager.StartTurnInterestCheck(
            gameState,
            playerId,
            LoanSharkConfig.FromRules(gameState.Rules.Loans));
        var player = afterLoanInterest.Players.Single(candidate => candidate.PlayerId == playerId);

        return player.IsEliminated
            ? afterLoanInterest
            : PropertyStateManager.RepairDamagedOwnedProperties(afterLoanInterest, playerId);
    }

    private static GameState ApplyStartTurnEffectsAndSkipEliminatedNonTerminalPlayers(
        GameState gameState,
        PlayerId playerId)
    {
        var afterStartTurnEffects = ApplyStartTurnEffects(gameState, playerId);
        var player = afterStartTurnEffects.Players.Single(candidate => candidate.PlayerId == playerId);
        if (!player.IsEliminated || CountActivePlayers(afterStartTurnEffects.Players) <= 1)
        {
            return afterStartTurnEffects;
        }

        var currentIndex = FindCurrentPlayerIndex(afterStartTurnEffects.Players, playerId);
        var nextIndex = FindNextActivePlayerIndex(
            afterStartTurnEffects.Players,
            currentIndex,
            afterStartTurnEffects.Rules.Jail.Enabled);
        var nextPlayerId = afterStartTurnEffects.Players[nextIndex].PlayerId;

        return ApplyStartTurnEffectsAndSkipEliminatedNonTerminalPlayers(
            afterStartTurnEffects with { CurrentTurnPlayerId = nextPlayerId },
            nextPlayerId);
    }

    private static int FindCurrentPlayerIndex(IReadOnlyList<Player> players, PlayerId currentTurnPlayerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == currentTurnPlayerId)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Current turn player must exist in the game player list.");
    }

    private static int FindNextActivePlayerIndex(
        IReadOnlyList<Player> players,
        int currentIndex,
        bool lockupTurnsEnabled)
    {
        for (var offset = 1; offset <= players.Count; offset++)
        {
            var nextIndex = (currentIndex + offset) % players.Count;
            if (IsSelectableActive(players[nextIndex], lockupTurnsEnabled))
            {
                return nextIndex;
            }
        }

        for (var offset = 1; offset <= players.Count; offset++)
        {
            var nextIndex = (currentIndex + offset) % players.Count;
            if (IsActive(players[nextIndex]))
            {
                return nextIndex;
            }
        }

        throw new InvalidOperationException("A game must have at least one active player before turns can advance.");
    }

    private static Player? SelectFirstTurnPlayer(IReadOnlyList<Player> players, bool lockupTurnsEnabled)
    {
        return players.FirstOrDefault(player => IsSelectableActive(player, lockupTurnsEnabled)) ??
            players.FirstOrDefault(IsActive);
    }

    private static bool IsSelectableActive(Player player, bool lockupTurnsEnabled)
    {
        return IsActive(player) && (lockupTurnsEnabled || !player.IsLockedUp);
    }

    private static bool IsActive(Player player)
    {
        return !player.IsEliminated;
    }

    private static int CountActivePlayers(IReadOnlyList<Player> players)
    {
        return players.Count(IsActive);
    }
}
