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
            Array.Empty<AuctionBid>(),
            HighestBid: null,
            HighestBidderId: null,
            CountdownDurationSeconds: null);

        return new AuctionStartResult(
            AuctionStartResultKind.Started,
            auctionState,
            "Mandatory auction started.");
    }

    public static AuctionBidResult PlaceBid(
        GameState gameState,
        AuctionState auctionState,
        PlayerId bidderId,
        Money amount,
        DateTimeOffset placedAtUtc)
    {
        var bidder = FindPlayerOrNull(gameState.Players, bidderId);
        if (bidder is null)
        {
            return BidRejected(
                AuctionBidResultKind.BidderNotInGame,
                auctionState,
                "Auction bidder must exist in the game player list.");
        }

        if (bidder.IsEliminated)
        {
            return BidRejected(
                AuctionBidResultKind.BidderEliminated,
                auctionState,
                "Eliminated players cannot bid in auctions.");
        }

        if (auctionState.Bids.Count == 0)
        {
            if (amount.Amount < auctionState.StartingBid.Amount)
            {
                return BidRejected(
                    AuctionBidResultKind.BidBelowStartingBid,
                    auctionState,
                    "First auction bid must meet or exceed the starting bid.");
            }
        }
        else
        {
            var currentHighestBid = auctionState.HighestBid ?? FindHighestBid(auctionState.Bids);
            var minimumAllowedBid = new Money(currentHighestBid.Amount + auctionState.MinimumBidIncrement.Amount);
            if (amount.Amount < minimumAllowedBid.Amount)
            {
                return BidRejected(
                    AuctionBidResultKind.BidBelowMinimumIncrement,
                    auctionState,
                    "Auction bid must meet or exceed the current highest bid plus the minimum increment.");
            }
        }

        var bid = new AuctionBid(bidderId, amount, placedAtUtc);
        var bids = auctionState.Bids.Concat(new[] { bid }).ToArray();
        var nextState = auctionState with
        {
            Status = AuctionStatus.ActiveBidCountdown,
            Bids = bids,
            HighestBid = amount,
            HighestBidderId = bidderId,
            CountdownDurationSeconds = auctionState.BidResetSeconds,
        };

        return new AuctionBidResult(AuctionBidResultKind.Accepted, nextState, "Auction bid accepted.");
    }

    private static AuctionStartResult NoAuction(AuctionStartResultKind resultKind, string message)
    {
        return new AuctionStartResult(resultKind, AuctionState: null, message);
    }

    private static AuctionBidResult BidRejected(
        AuctionBidResultKind resultKind,
        AuctionState auctionState,
        string message)
    {
        return new AuctionBidResult(resultKind, auctionState, message);
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

    private static Player? FindPlayerOrNull(IReadOnlyList<Player> players, PlayerId playerId)
    {
        foreach (var player in players)
        {
            if (player.PlayerId == playerId)
            {
                return player;
            }
        }

        return null;
    }

    private static Money FindHighestBid(IReadOnlyList<AuctionBid> bids)
    {
        var highestBid = bids[0].Amount;
        for (var index = 1; index < bids.Count; index++)
        {
            if (bids[index].Amount.Amount > highestBid.Amount)
            {
                highestBid = bids[index].Amount;
            }
        }

        return highestBid;
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
