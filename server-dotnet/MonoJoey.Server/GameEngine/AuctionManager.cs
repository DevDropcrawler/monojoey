namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class AuctionManager
{
    private const string InvalidAuctionStateMessage = "Auction state is invalid and cannot be finalized.";

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

    public static AuctionFinalizationResult FinalizeAuction(GameState gameState, AuctionState auctionState)
    {
        if (!IsKnownAuctionStatus(auctionState.Status))
        {
            return FinalizationFailed(gameState, auctionState);
        }

        var propertyTile = FindTileByIdOrNull(gameState.Board, auctionState.PropertyTileId);
        if (propertyTile is null || !propertyTile.IsPurchasable || !propertyTile.IsAuctionable)
        {
            return FinalizationFailed(gameState, auctionState);
        }

        if (FindPropertyOwnerIndex(gameState.Players, auctionState.PropertyTileId) is not null)
        {
            return FinalizationFailed(gameState, auctionState);
        }

        if (auctionState.Bids.Count == 0)
        {
            return new AuctionFinalizationResult(
                AuctionFinalizationResultKind.FinalizedNoWinner,
                gameState,
                auctionState,
                auctionState.PropertyTileId,
                WinnerId: null,
                WinningBid: null,
                EliminationResult: null,
                "Auction finalized with no winner.");
        }

        var winner = FindHighestEligibleBid(gameState.Players, auctionState.Bids);
        if (winner is null)
        {
            return new AuctionFinalizationResult(
                AuctionFinalizationResultKind.FinalizedNoWinner,
                gameState,
                auctionState,
                auctionState.PropertyTileId,
                WinnerId: null,
                WinningBid: null,
                EliminationResult: null,
                "Auction finalized with no eligible winner.");
        }

        var (winnerIndex, winningBid) = winner.Value;
        var winningPlayer = gameState.Players[winnerIndex];
        if (winningPlayer.Money.Amount < winningBid.Amount)
        {
            var eliminationResult = BankruptcyManager.EliminateForFailedPayment(
                gameState,
                winningPlayer.PlayerId,
                winningBid);

            return new AuctionFinalizationResult(
                AuctionFinalizationResultKind.WinnerFailedToPay,
                eliminationResult.GameState,
                auctionState,
                auctionState.PropertyTileId,
                winningPlayer.PlayerId,
                winningBid,
                eliminationResult,
                "Auction winner could not pay the winning bid.");
        }

        var players = gameState.Players.ToArray();
        players[winnerIndex] = winningPlayer with
        {
            Money = new Money(winningPlayer.Money.Amount - winningBid.Amount),
        };
        var paidGameState = gameState with { Players = players };
        var finalizedGameState = PropertyManager.AssignOwner(
            paidGameState,
            auctionState.PropertyTileId,
            winningPlayer.PlayerId);

        return new AuctionFinalizationResult(
            AuctionFinalizationResultKind.FinalizedWithWinner,
            finalizedGameState,
            auctionState,
            auctionState.PropertyTileId,
            winningPlayer.PlayerId,
            winningBid,
            EliminationResult: null,
            "Auction finalized with a winning bidder.");
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

    private static AuctionFinalizationResult FinalizationFailed(GameState gameState, AuctionState auctionState)
    {
        return new AuctionFinalizationResult(
            AuctionFinalizationResultKind.InvalidAuctionState,
            gameState,
            auctionState,
            auctionState.PropertyTileId,
            WinnerId: null,
            WinningBid: null,
            EliminationResult: null,
            InvalidAuctionStateMessage);
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

    private static (int WinnerIndex, Money WinningBid)? FindHighestEligibleBid(
        IReadOnlyList<Player> players,
        IReadOnlyList<AuctionBid> bids)
    {
        (int WinnerIndex, Money WinningBid)? winner = null;
        foreach (var bid in bids)
        {
            var bidderIndex = FindPlayerIndexOrNull(players, bid.BidderId);
            if (bidderIndex is null || players[bidderIndex.Value].IsEliminated)
            {
                continue;
            }

            if (winner is null || bid.Amount.Amount > winner.Value.WinningBid.Amount)
            {
                winner = (bidderIndex.Value, bid.Amount);
            }
        }

        return winner;
    }

    private static bool IsKnownAuctionStatus(AuctionStatus status)
    {
        return status is AuctionStatus.AwaitingInitialBid or AuctionStatus.ActiveBidCountdown;
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

    private static Tile? FindTileByIdOrNull(Board board, TileId tileId)
    {
        foreach (var tile in board.Tiles)
        {
            if (tile.TileId == tileId)
            {
                return tile;
            }
        }

        return null;
    }

    private static int? FindPlayerIndexOrNull(IReadOnlyList<Player> players, PlayerId playerId)
    {
        for (var index = 0; index < players.Count; index++)
        {
            if (players[index].PlayerId == playerId)
            {
                return index;
            }
        }

        return null;
    }
}
