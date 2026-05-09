namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;

public static class SolvencyAnalyzer
{
    public static SolvencyAnalysisResult Analyze(GameState gameState, PaymentObligation obligation)
    {
        var obligationValidation = ValidateObligation(obligation);
        if (obligationValidation is not null)
        {
            return AnalysisRejected(
                SolvencyAnalysisResultKind.InvalidObligation,
                obligation,
                Money.Zero,
                EmptyRaiseable(obligation.DebtorPlayerId),
                obligationValidation);
        }

        var player = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);
        if (player is null)
        {
            return AnalysisRejected(
                SolvencyAnalysisResultKind.PlayerNotInGame,
                obligation,
                Money.Zero,
                EmptyRaiseable(obligation.DebtorPlayerId),
                "Payment obligation debtor is not in the game.");
        }

        if (player.IsBankrupt)
        {
            return AnalysisRejected(
                SolvencyAnalysisResultKind.PlayerBankrupt,
                obligation,
                player.Money,
                EmptyRaiseable(obligation.DebtorPlayerId, RaiseableCashResultKind.PlayerBankrupt),
                "Bankrupt players cannot satisfy payment obligations.");
        }

        if (player.IsEliminated)
        {
            return AnalysisRejected(
                SolvencyAnalysisResultKind.PlayerEliminated,
                obligation,
                player.Money,
                EmptyRaiseable(obligation.DebtorPlayerId, RaiseableCashResultKind.PlayerEliminated),
                "Eliminated players cannot satisfy payment obligations.");
        }

        var cashOnHand = player.Money;
        var raiseableCash = CalculateRaiseableCash(gameState, obligation.DebtorPlayerId);
        if (cashOnHand.Amount >= obligation.Amount.Amount)
        {
            var cashSolventTotalAvailable = CalculateTotalAvailable(cashOnHand, raiseableCash);
            return new SolvencyAnalysisResult(
                SolvencyAnalysisResultKind.SolventWithCash,
                obligation,
                cashOnHand,
                raiseableCash,
                cashSolventTotalAvailable,
                Money.Zero,
                raiseableCash.Items,
                "Payment obligation can be satisfied with cash on hand.");
        }

        if (raiseableCash.ResultKind != RaiseableCashResultKind.Calculated)
        {
            return AnalysisRejected(
                MapRaiseableFailure(raiseableCash.ResultKind),
                obligation,
                cashOnHand,
                raiseableCash,
                raiseableCash.Message);
        }

        var totalAvailableAmount = (long)cashOnHand.Amount + raiseableCash.TotalRaiseableCash.Amount;
        if (totalAvailableAmount > int.MaxValue)
        {
            return AnalysisRejected(
                SolvencyAnalysisResultKind.UnsafeMoneyTotal,
                obligation,
                cashOnHand,
                raiseableCash,
                "Solvency analysis total exceeds the safe money balance.");
        }

        var totalAvailable = new Money((int)totalAvailableAmount);
        var shortfallAmount = Math.Max(0, obligation.Amount.Amount - totalAvailable.Amount);
        if (shortfallAmount == 0)
        {
            return new SolvencyAnalysisResult(
                SolvencyAnalysisResultKind.SolventWithAssets,
                obligation,
                cashOnHand,
                raiseableCash,
                totalAvailable,
                Money.Zero,
                raiseableCash.Items,
                "Payment obligation can be satisfied after raising cash from assets.");
        }

        return new SolvencyAnalysisResult(
            SolvencyAnalysisResultKind.Insolvent,
            obligation,
            cashOnHand,
            raiseableCash,
            totalAvailable,
            new Money(shortfallAmount),
            raiseableCash.Items,
            "Payment obligation cannot be satisfied from cash and raiseable assets.");
    }

    public static RaiseableCashResult CalculateRaiseableCash(GameState gameState, PlayerId playerId)
    {
        var player = PropertyRuleHelpers.FindPlayer(gameState.Players, playerId);
        if (player is null)
        {
            return EmptyRaiseable(playerId, RaiseableCashResultKind.PlayerNotInGame, "Player is not in the game.");
        }

        if (player.IsBankrupt)
        {
            return EmptyRaiseable(playerId, RaiseableCashResultKind.PlayerBankrupt, "Bankrupt players cannot raise cash.");
        }

        if (player.IsEliminated)
        {
            return EmptyRaiseable(playerId, RaiseableCashResultKind.PlayerEliminated, "Eliminated players cannot raise cash.");
        }

        var items = new List<RaiseableCashItem>();
        var ownedTiles = gameState.Board.Tiles
            .Where(tile => player.OwnedPropertyIds.Contains(tile.TileId))
            .OrderBy(tile => tile.Index)
            .ThenBy(tile => tile.TileId.Value, StringComparer.Ordinal);

        foreach (var tile in ownedTiles)
        {
            var propertyStateData = PropertyRuleHelpers.GetPropertyStateData(gameState, tile.TileId);
            var upgradeSaleItem = CreateUpgradeSaleItem(gameState, player, tile, propertyStateData);
            if (upgradeSaleItem is not null)
            {
                items.Add(upgradeSaleItem);
            }

            var mortgageItem = CreateMortgageItem(gameState, tile, propertyStateData, upgradeSaleItem is not null);
            if (mortgageItem is not null)
            {
                items.Add(mortgageItem);
            }
        }

        long total = 0;
        foreach (var item in items)
        {
            total += item.Amount.Amount;
            if (total > int.MaxValue)
            {
                return new RaiseableCashResult(
                    RaiseableCashResultKind.UnsafeMoneyTotal,
                    playerId,
                    Money.Zero,
                    items,
                    "Raiseable cash total exceeds the safe money balance.");
            }
        }

        return new RaiseableCashResult(
            RaiseableCashResultKind.Calculated,
            playerId,
            new Money((int)total),
            items,
            "Raiseable cash calculated.");
    }

    private static RaiseableCashItem? CreateUpgradeSaleItem(
        GameState gameState,
        Player player,
        Tile tile,
        PropertyStateData propertyStateData)
    {
        if (!gameState.Rules.Economy.UpgradesEnabled ||
            propertyStateData.UpgradeLevel <= 0 ||
            !PropertyRuleHelpers.IsBuildableProperty(tile) ||
            tile.UpgradeCost is null)
        {
            return null;
        }

        var groupTiles = PropertyRuleHelpers.GetBuildableGroupTiles(gameState.Board, tile);
        if (groupTiles.Count < 2 ||
            groupTiles.Any(groupTile => !player.OwnedPropertyIds.Contains(groupTile.TileId)) ||
            groupTiles.Any(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).IsMortgaged))
        {
            return null;
        }

        var refundPerUpgrade = EconomyRulesCalculator.CalculateUpgradeSellRefund(tile.UpgradeCost.Value, gameState.Rules.Economy);
        return new RaiseableCashItem(
            RaiseableCashSource.UpgradeSale,
            tile.TileId,
            new Money(refundPerUpgrade.Amount * propertyStateData.UpgradeLevel),
            propertyStateData.UpgradeLevel,
            propertyStateData.UpgradeLevel,
            RequiresUpgradeLiquidation: false);
    }

    private static RaiseableCashItem? CreateMortgageItem(
        GameState gameState,
        Tile tile,
        PropertyStateData propertyStateData,
        bool upgradeLiquidationAvailable)
    {
        if (!gameState.Rules.Economy.MortgagesEnabled ||
            !tile.IsPurchasable ||
            tile.Price is null ||
            propertyStateData.IsMortgaged ||
            propertyStateData.UpgradeLevel > 0 && !upgradeLiquidationAvailable)
        {
            return null;
        }

        var mortgageValue = EconomyRulesCalculator.CalculateMortgageValue(tile.Price.Value, gameState.Rules.Economy);
        return new RaiseableCashItem(
            RaiseableCashSource.Mortgage,
            tile.TileId,
            mortgageValue,
            UnitCount: 1,
            propertyStateData.UpgradeLevel,
            RequiresUpgradeLiquidation: propertyStateData.UpgradeLevel > 0);
    }

    private static string? ValidateObligation(PaymentObligation obligation)
    {
        if (obligation.Amount.Amount < 0)
        {
            return "Payment obligation amount must be non-negative.";
        }

        if (!Enum.IsDefined(obligation.Kind))
        {
            return "Payment obligation kind is not supported.";
        }

        if (!Enum.IsDefined(obligation.Creditor.Kind))
        {
            return "Payment obligation creditor kind is not supported.";
        }

        if (obligation.Creditor.Kind == PaymentObligationCreditorKind.Player &&
            obligation.Creditor.PlayerId is null)
        {
            return "Player creditor obligations must include a creditor player.";
        }

        return null;
    }

    private static SolvencyAnalysisResult AnalysisRejected(
        SolvencyAnalysisResultKind resultKind,
        PaymentObligation obligation,
        Money cashOnHand,
        RaiseableCashResult raiseableCash,
        string message)
    {
        return new SolvencyAnalysisResult(
            resultKind,
            obligation,
            cashOnHand,
            raiseableCash,
            cashOnHand,
            obligation.Amount.Amount > cashOnHand.Amount
                ? new Money(obligation.Amount.Amount - cashOnHand.Amount)
                : Money.Zero,
            raiseableCash.Items,
            message);
    }

    private static RaiseableCashResult EmptyRaiseable(
        PlayerId playerId,
        RaiseableCashResultKind resultKind = RaiseableCashResultKind.Calculated,
        string message = "No raiseable cash calculated.")
    {
        return new RaiseableCashResult(
            resultKind,
            playerId,
            Money.Zero,
            Array.Empty<RaiseableCashItem>(),
            message);
    }

    private static SolvencyAnalysisResultKind MapRaiseableFailure(RaiseableCashResultKind resultKind)
    {
        return resultKind switch
        {
            RaiseableCashResultKind.PlayerNotInGame => SolvencyAnalysisResultKind.PlayerNotInGame,
            RaiseableCashResultKind.PlayerBankrupt => SolvencyAnalysisResultKind.PlayerBankrupt,
            RaiseableCashResultKind.PlayerEliminated => SolvencyAnalysisResultKind.PlayerEliminated,
            RaiseableCashResultKind.UnsafeMoneyTotal => SolvencyAnalysisResultKind.UnsafeMoneyTotal,
            _ => throw new InvalidOperationException("Calculated raiseable cash is not a solvency failure."),
        };
    }

    private static Money CalculateTotalAvailable(Money cashOnHand, RaiseableCashResult raiseableCash)
    {
        if (raiseableCash.ResultKind != RaiseableCashResultKind.Calculated)
        {
            return cashOnHand;
        }

        var totalAvailableAmount = (long)cashOnHand.Amount + raiseableCash.TotalRaiseableCash.Amount;
        return totalAvailableAmount > int.MaxValue
            ? cashOnHand
            : new Money((int)totalAvailableAmount);
    }
}
