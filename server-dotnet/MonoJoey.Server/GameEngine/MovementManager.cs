namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class MovementManager
{
    public static MovementResult MovePlayer(GameState gameState, PlayerId playerId, int steps)
    {
        if (gameState.Board.Tiles.Count == 0)
        {
            throw new InvalidOperationException("A board must have at least one tile before players can move.");
        }

        var playerIndex = FindPlayerIndex(gameState.Players, playerId);
        var player = gameState.Players[playerIndex];
        var currentTile = FindTileById(gameState.Board, player.CurrentTileId);
        var boardLength = gameState.Board.Tiles.Count;
        var landingTileIndex = PositiveModulo(currentTile.Index + steps, boardLength);
        var landingTile = FindTileByIndex(gameState.Board, landingTileIndex);
        var pathTileIds = CalculatePathTileIds(gameState.Board, currentTile.Index, steps);
        var movedPlayer = player with { CurrentTileId = landingTile.TileId };
        var players = gameState.Players.ToArray();
        players[playerIndex] = movedPlayer;

        return new MovementResult(
            gameState with { Players = players },
            playerId,
            currentTile.TileId,
            landingTile.TileId,
            landingTile.TileId,
            landingTileIndex,
            pathTileIds,
            steps,
            "path",
            steps > 0 && currentTile.Index + steps >= boardLength);
    }

    private static IReadOnlyList<TileId> CalculatePathTileIds(Board board, int fromTileIndex, int steps)
    {
        if (steps == 0)
        {
            return Array.Empty<TileId>();
        }

        var direction = steps > 0 ? 1 : -1;
        var enteredTileCount = Math.Abs(steps);
        var pathTileIds = new TileId[enteredTileCount];

        for (var offset = 1; offset <= enteredTileCount; offset++)
        {
            var tileIndex = PositiveModulo(fromTileIndex + (offset * direction), board.Tiles.Count);
            pathTileIds[offset - 1] = FindTileByIndex(board, tileIndex).TileId;
        }

        return pathTileIds;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
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

        throw new InvalidOperationException("Player must exist in the game player list before they can move.");
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

        throw new InvalidOperationException("Player current tile must exist on the board before they can move.");
    }

    private static Tile FindTileByIndex(Board board, int tileIndex)
    {
        foreach (var tile in board.Tiles)
        {
            if (tile.Index == tileIndex)
            {
                return tile;
            }
        }

        throw new InvalidOperationException("Landing tile index must exist on the board.");
    }
}
