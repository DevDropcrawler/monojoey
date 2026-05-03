namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class PlayerStatusEffectManager
{
    public const string SlimerDefinitionId = "slimer";

    public static bool HasSlimer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return player.StatusEffects.Any(statusEffect => statusEffect.Kind == PlayerStatusEffectKind.Slimer);
    }

    public static GameState ApplySlimer(GameState gameState, PlayerId playerId, string? sourceId = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        if (HasSlimer(player))
        {
            return gameState;
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            StatusEffects = player.StatusEffects
                .Append(CreateSlimerStatusEffect(playerId, sourceId))
                .ToArray(),
        };

        return gameState with { Players = players };
    }

    public static GameState RemoveSlimer(GameState gameState, PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        if (!HasSlimer(player))
        {
            return gameState;
        }

        var players = gameState.Players.ToArray();
        players[playerIndex] = player with
        {
            StatusEffects = player.StatusEffects
                .Where(statusEffect => statusEffect.Kind != PlayerStatusEffectKind.Slimer)
                .ToArray(),
        };

        return gameState with { Players = players };
    }

    private static PlayerStatusEffect CreateSlimerStatusEffect(PlayerId playerId, string? sourceId)
    {
        return new PlayerStatusEffect(
            $"slimer:{playerId.Value}",
            PlayerStatusEffectKind.Slimer,
            new PlayerStatusEffectData(
                SlimerDefinitionId,
                StackCount: 1,
                RemainingTurns: null,
                SourceId: sourceId));
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

        throw new InvalidOperationException("Player must exist in the game player list before status effects can change.");
    }
}
