namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class TileResolver
{
    public static TileResolutionResult ResolveCurrentTile(GameState gameState, PlayerId playerId)
    {
        if (gameState.Board.Tiles.Count == 0)
        {
            throw new InvalidOperationException("A board must have at least one tile before resolving tile effects.");
        }

        var player = FindPlayer(gameState.Players, playerId);
        var tile = FindTileById(gameState.Board, player.CurrentTileId);
        var actionKind = GetActionKind(tile);
        var requiresAction = actionKind is not TileResolutionActionKind.NoAction
            and not TileResolutionActionKind.StartPlaceholder;

        return new TileResolutionResult(
            player.PlayerId,
            tile.TileId,
            tile.Index,
            tile.TileType,
            requiresAction,
            actionKind);
    }

    private static Player FindPlayer(IReadOnlyList<Player> players, PlayerId playerId)
    {
        foreach (var player in players)
        {
            if (player.PlayerId == playerId)
            {
                return player;
            }
        }

        throw new InvalidOperationException("Player must exist in the game player list before resolving a tile.");
    }

    private static Tile FindTileById(Board board, TileId tileId)
    {
        foreach (var tile in board.Tiles)
        {
            if (tile.TileId == tileId)
            {
                return tile;
            }
        }

        throw new InvalidOperationException("Player current tile must exist on the board before resolving a tile.");
    }

    private static TileResolutionActionKind GetActionKind(Tile tile)
    {
        if (tile.IsPurchasable)
        {
            return TileResolutionActionKind.PropertyPlaceholder;
        }

        return tile.TileType switch
        {
            TileType.Start => TileResolutionActionKind.StartPlaceholder,
            TileType.ChanceDeck or TileType.TableDeck => TileResolutionActionKind.DeckPlaceholder,
            TileType.Tax => TileResolutionActionKind.TaxPlaceholder,
            TileType.GoToLockup => TileResolutionActionKind.GoToLockupPlaceholder,
            _ => TileResolutionActionKind.NoAction,
        };
    }
}
