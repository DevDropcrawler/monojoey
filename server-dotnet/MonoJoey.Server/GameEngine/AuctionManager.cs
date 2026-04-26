namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class AuctionManager
{
    public static AuctionStartResult StartMandatoryAuction(
        GameState gameState,
        PlayerId triggeringPlayerId,
        TileId propertyTileId,
        AuctionConfig? config = null)
    {
        var auctionConfig = config ?? AuctionConfig.Default;
        if (!auctionConfig.MandatoryAuctionsEnabled)
        {
            return NoAuction(
                AuctionStartResultKind.MandatoryAuctionsDisabled,
                "Mandatory auctions are disabled.");
        }

        _ = FindPlayerIndex(gameState.Players, triggeringPlayerId);
        var tile = FindTileById(gameState.Board, propertyTileId);

        if (!tile.IsPurchasable || !tile.IsAuctionable)
        {
            return NoAuction(
                AuctionStartResultKind.TileNotAuctionable,
                "Tile is not eligible for a mandatory auction.");
        }

        if (FindPropertyOwnerIndex(gameState.Players, propertyTileId) is not null)
        {
            return NoAuction(
                AuctionStartResultKind.PropertyAlreadyOwned,
                "Property is already owned.");
        }

        var auctionState = new AuctionState(
            propertyTileId,
            triggeringPlayerId,
            AuctionStatus.AwaitingInitialBid,
            auctionConfig.StartingBid,
            auctionConfig.MinimumBidIncrement,
            auctionConfig.InitialPreBidSeconds,
            auctionConfig.BidResetSeconds,
            Array.Empty<AuctionBid>());

        return new AuctionStartResult(
            AuctionStartResultKind.Started,
            auctionState,
            "Mandatory auction started.");
    }

    private static AuctionStartResult NoAuction(AuctionStartResultKind resultKind, string message)
    {
        return new AuctionStartResult(resultKind, AuctionState: null, message);
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

        throw new InvalidOperationException("Auction triggering player must exist in the game player list.");
    }

    private static int? FindPropertyOwnerIndex(IReadOnlyList<Player> players, TileId propertyTileId)
    {
        int? ownerIndex = null;
        for (var index = 0; index < players.Count; index++)
        {
            if (!players[index].OwnedPropertyIds.Contains(propertyTileId))
            {
                continue;
            }

            if (ownerIndex is not null)
            {
                throw new InvalidOperationException("Property cannot be owned by multiple players.");
            }

            ownerIndex = index;
        }

        return ownerIndex;
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

        throw new InvalidOperationException("Auction tile must exist on the board.");
    }
}
