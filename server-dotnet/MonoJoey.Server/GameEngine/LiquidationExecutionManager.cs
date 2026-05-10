namespace MonoJoey.Server.GameEngine;

using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public static class LiquidationExecutionManager
{
    public static LiquidationExecutionResult ExecutePaymentObligation(
        GameState gameState,
        PaymentObligation obligation)
    {
        return ExecutePaymentObligation(gameState, obligation, LiquidationExecutionContext.Normal);
    }

    public static MultiCreditorLiquidationExecutionResult ExecuteMultiCreditorPaymentObligation(
        GameState gameState,
        MultiCreditorPaymentObligation obligation)
    {
        return ExecuteMultiCreditorPaymentObligation(gameState, obligation, LiquidationExecutionContext.Normal);
    }

    internal static LiquidationExecutionResult ExecutePaymentObligation(
        GameState gameState,
        PaymentObligation obligation,
        LiquidationExecutionContext context)
    {
        var validation = ValidateRequest(gameState, obligation, context);
        if (validation is not null)
        {
            return Rejected(validation.Value.Kind, gameState, obligation, validation.Value.Message);
        }

        var workingState = gameState;
        var steps = new List<LiquidationStepResult>();
        var debtorId = obligation.DebtorPlayerId;
        var debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, debtorId)
            ?? throw new InvalidOperationException("Validated liquidation debtor must exist.");

        while (debtor.Money.Amount < obligation.Amount.Amount)
        {
            var upgradeCandidate = FindNextUpgradeSaleCandidate(workingState, debtor);
            if (upgradeCandidate is not null)
            {
                var stateBeforeSale = workingState;
                var sale = PropertyUpgradeManager.SellUpgrade(
                    CreateAssetLiquidationState(workingState, context),
                    debtorId,
                    upgradeCandidate.TileId);
                if (!sale.UpgradeSold)
                {
                    return Rejected(
                        sale.ResultKind == PropertyUpgradeResultKind.UnsafeMoneyBalance
                            ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                            : LiquidationExecutionResultKind.UnsupportedPayment,
                        gameState,
                        obligation,
                        sale.Message);
                }

                workingState = RestoreTileExecutionState(sale.GameState, stateBeforeSale, context);
                steps.Add(new LiquidationStepResult(
                    LiquidationStepKind.UpgradeSale,
                    upgradeCandidate.TileId,
                    sale.RefundAmount,
                    sale.Money,
                    sale.UpgradeLevel,
                    PropertyRuleHelpers.GetPropertyStateData(workingState, upgradeCandidate.TileId).IsMortgaged));
                debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, debtorId)
                    ?? throw new InvalidOperationException("Liquidation debtor disappeared after upgrade sale.");
                continue;
            }

            var mortgageCandidate = FindNextMortgageCandidate(workingState, debtor);
            if (mortgageCandidate is not null)
            {
                var stateBeforeMortgage = workingState;
                var mortgage = MortgageManager.MortgageProperty(
                    CreateAssetLiquidationState(workingState, context),
                    debtorId,
                    mortgageCandidate.TileId);
                if (!mortgage.MortgageAccepted)
                {
                    return Rejected(
                        mortgage.ResultKind == MortgageResultKind.UnsafeMoneyBalance
                            ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                            : LiquidationExecutionResultKind.UnsupportedPayment,
                        gameState,
                        obligation,
                        mortgage.Message);
                }

                workingState = RestoreTileExecutionState(mortgage.GameState, stateBeforeMortgage, context);
                steps.Add(new LiquidationStepResult(
                    LiquidationStepKind.Mortgage,
                    mortgageCandidate.TileId,
                    mortgage.MortgageValue,
                    mortgage.Money,
                    PropertyRuleHelpers.GetPropertyStateData(workingState, mortgageCandidate.TileId).UpgradeLevel,
                    mortgage.IsMortgaged));
                debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, debtorId)
                    ?? throw new InvalidOperationException("Liquidation debtor disappeared after mortgage.");
                continue;
            }

            return Rejected(
                LiquidationExecutionResultKind.Insolvent,
                gameState,
                obligation,
                "Payment obligation cannot be satisfied from cash and legal liquidation.");
        }

        return obligation.Creditor.Kind switch
        {
            PaymentObligationCreditorKind.Bank => PayBank(gameState, workingState, obligation, steps),
            PaymentObligationCreditorKind.Player => PayPlayer(gameState, workingState, obligation, steps),
            _ => Rejected(
                LiquidationExecutionResultKind.InvalidObligation,
                gameState,
                obligation,
                "Payment obligation creditor kind is not supported."),
        };
    }

    internal static MultiCreditorLiquidationExecutionResult ExecuteMultiCreditorPaymentObligation(
        GameState gameState,
        MultiCreditorPaymentObligation obligation,
        LiquidationExecutionContext context)
    {
        var validation = ValidateRequest(gameState, obligation, context);
        if (validation is not null)
        {
            return Rejected(validation.Value.Kind, gameState, obligation, Array.Empty<PlayerId>(), Money.Zero, validation.Value.Message);
        }

        var creditorPlayerIds = GetActiveCreditorPlayerIds(gameState, obligation.DebtorPlayerId);
        var amountDue = CalculateTotalDue(obligation.AmountPerCreditor, creditorPlayerIds.Count);
        if (amountDue is null)
        {
            return Rejected(
                LiquidationExecutionResultKind.UnsafeMoneyBalance,
                gameState,
                obligation,
                creditorPlayerIds,
                Money.Zero,
                "Payment obligation total would exceed the safe money balance.");
        }

        if (creditorPlayerIds.Count == 0)
        {
            var noCreditorDebtor = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);
            return new MultiCreditorLiquidationExecutionResult(
                LiquidationExecutionResultKind.PaymentExecuted,
                gameState,
                obligation,
                creditorPlayerIds,
                Array.Empty<LiquidationStepResult>(),
                amountDue.Value,
                Money.Zero,
                noCreditorDebtor?.Money ?? Money.Zero,
                new Dictionary<PlayerId, Money>(),
                "Payment obligation had no active creditors.");
        }

        var workingState = gameState;
        var steps = new List<LiquidationStepResult>();
        var debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, obligation.DebtorPlayerId)
            ?? throw new InvalidOperationException("Validated liquidation debtor must exist.");

        while (debtor.Money.Amount < amountDue.Value.Amount)
        {
            var upgradeCandidate = FindNextUpgradeSaleCandidate(workingState, debtor);
            if (upgradeCandidate is not null)
            {
                var stateBeforeSale = workingState;
                var sale = PropertyUpgradeManager.SellUpgrade(
                    CreateAssetLiquidationState(workingState, context),
                    obligation.DebtorPlayerId,
                    upgradeCandidate.TileId);
                if (!sale.UpgradeSold)
                {
                    return Rejected(
                        sale.ResultKind == PropertyUpgradeResultKind.UnsafeMoneyBalance
                            ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                            : LiquidationExecutionResultKind.UnsupportedPayment,
                        gameState,
                        obligation,
                        creditorPlayerIds,
                        amountDue.Value,
                        sale.Message);
                }

                workingState = RestoreTileExecutionState(sale.GameState, stateBeforeSale, context);
                steps.Add(new LiquidationStepResult(
                    LiquidationStepKind.UpgradeSale,
                    upgradeCandidate.TileId,
                    sale.RefundAmount,
                    sale.Money,
                    sale.UpgradeLevel,
                    PropertyRuleHelpers.GetPropertyStateData(workingState, upgradeCandidate.TileId).IsMortgaged));
                debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, obligation.DebtorPlayerId)
                    ?? throw new InvalidOperationException("Liquidation debtor disappeared after upgrade sale.");
                continue;
            }

            var mortgageCandidate = FindNextMortgageCandidate(workingState, debtor);
            if (mortgageCandidate is not null)
            {
                var stateBeforeMortgage = workingState;
                var mortgage = MortgageManager.MortgageProperty(
                    CreateAssetLiquidationState(workingState, context),
                    obligation.DebtorPlayerId,
                    mortgageCandidate.TileId);
                if (!mortgage.MortgageAccepted)
                {
                    return Rejected(
                        mortgage.ResultKind == MortgageResultKind.UnsafeMoneyBalance
                            ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                            : LiquidationExecutionResultKind.UnsupportedPayment,
                        gameState,
                        obligation,
                        creditorPlayerIds,
                        amountDue.Value,
                        mortgage.Message);
                }

                workingState = RestoreTileExecutionState(mortgage.GameState, stateBeforeMortgage, context);
                steps.Add(new LiquidationStepResult(
                    LiquidationStepKind.Mortgage,
                    mortgageCandidate.TileId,
                    mortgage.MortgageValue,
                    mortgage.Money,
                    PropertyRuleHelpers.GetPropertyStateData(workingState, mortgageCandidate.TileId).UpgradeLevel,
                    mortgage.IsMortgaged));
                debtor = PropertyRuleHelpers.FindPlayer(workingState.Players, obligation.DebtorPlayerId)
                    ?? throw new InvalidOperationException("Liquidation debtor disappeared after mortgage.");
                continue;
            }

            return Rejected(
                LiquidationExecutionResultKind.Insolvent,
                gameState,
                obligation,
                creditorPlayerIds,
                amountDue.Value,
                "Payment obligation cannot be satisfied from cash and legal liquidation.");
        }

        return PayActiveCreditors(gameState, workingState, obligation, creditorPlayerIds, amountDue.Value, steps);
    }

    private static LiquidationExecutionResult PayBank(
        GameState originalState,
        GameState workingState,
        PaymentObligation obligation,
        List<LiquidationStepResult> steps)
    {
        var debtorIndex = FindPlayerIndex(workingState.Players, obligation.DebtorPlayerId)
            ?? throw new InvalidOperationException("Validated liquidation debtor must exist.");
        var debtor = workingState.Players[debtorIndex];
        if (debtor.Money.Amount < obligation.Amount.Amount)
        {
            return Rejected(
                LiquidationExecutionResultKind.Insolvent,
                originalState,
                obligation,
                "Payment obligation cannot be satisfied from cash and legal liquidation.");
        }

        var players = workingState.Players.ToArray();
        players[debtorIndex] = debtor with
        {
            Money = new Money(debtor.Money.Amount - obligation.Amount.Amount),
        };

        var paidState = workingState with { Players = players };
        steps.Add(new LiquidationStepResult(
            LiquidationStepKind.BankPayment,
            PropertyTileId: null,
            obligation.Amount,
            players[debtorIndex].Money,
            UpgradeLevel: null,
            IsMortgaged: null));

        return new LiquidationExecutionResult(
            LiquidationExecutionResultKind.PaymentExecuted,
            paidState,
            obligation,
            steps.ToArray(),
            obligation.Amount,
            players[debtorIndex].Money,
            CreditorBalance: null,
            "Payment obligation paid to bank.");
    }

    private static LiquidationExecutionResult PayPlayer(
        GameState originalState,
        GameState workingState,
        PaymentObligation obligation,
        List<LiquidationStepResult> steps)
    {
        var creditorId = obligation.Creditor.PlayerId
            ?? throw new InvalidOperationException("Validated player creditor must exist.");
        var transfer = PlayerCashTransferManager.TransferBetweenPlayers(
            workingState,
            obligation.DebtorPlayerId,
            creditorId,
            obligation.Amount);
        if (!transfer.TransferAccepted)
        {
            return Rejected(
                transfer.ResultKind == PlayerCashTransferResultKind.UnsafeMoneyBalance
                    ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                    : LiquidationExecutionResultKind.UnsupportedPayment,
                originalState,
                obligation,
                transfer.Message);
        }

        steps.Add(new LiquidationStepResult(
            LiquidationStepKind.PlayerPayment,
            PropertyTileId: null,
            obligation.Amount,
            transfer.FromPlayerBalance,
            UpgradeLevel: null,
            IsMortgaged: null,
            creditorId));

        return new LiquidationExecutionResult(
            LiquidationExecutionResultKind.PaymentExecuted,
            transfer.GameState,
            obligation,
            steps.ToArray(),
            obligation.Amount,
            transfer.FromPlayerBalance,
            transfer.ToPlayerBalance,
            "Payment obligation paid to player.");
    }

    private static MultiCreditorLiquidationExecutionResult PayActiveCreditors(
        GameState originalState,
        GameState workingState,
        MultiCreditorPaymentObligation obligation,
        IReadOnlyList<PlayerId> creditorPlayerIds,
        Money amountDue,
        List<LiquidationStepResult> steps)
    {
        var paidState = workingState;
        foreach (var creditorPlayerId in creditorPlayerIds)
        {
            var transfer = PlayerCashTransferManager.TransferBetweenPlayers(
                paidState,
                obligation.DebtorPlayerId,
                creditorPlayerId,
                obligation.AmountPerCreditor);
            if (!transfer.TransferAccepted)
            {
                return Rejected(
                    transfer.ResultKind == PlayerCashTransferResultKind.UnsafeMoneyBalance
                        ? LiquidationExecutionResultKind.UnsafeMoneyBalance
                        : LiquidationExecutionResultKind.UnsupportedPayment,
                    originalState,
                    obligation,
                    creditorPlayerIds,
                    amountDue,
                    transfer.Message);
            }

            paidState = transfer.GameState;
            steps.Add(new LiquidationStepResult(
                LiquidationStepKind.PlayerPayment,
                PropertyTileId: null,
                obligation.AmountPerCreditor,
                transfer.FromPlayerBalance,
                UpgradeLevel: null,
                IsMortgaged: null,
                creditorPlayerId));
        }

        var debtor = PropertyRuleHelpers.FindPlayer(paidState.Players, obligation.DebtorPlayerId)
            ?? throw new InvalidOperationException("Validated liquidation debtor must exist.");
        return new MultiCreditorLiquidationExecutionResult(
            LiquidationExecutionResultKind.PaymentExecuted,
            paidState,
            obligation,
            creditorPlayerIds,
            steps.ToArray(),
            amountDue,
            amountDue,
            debtor.Money,
            GetCreditorBalances(paidState, creditorPlayerIds),
            "Payment obligation paid to active player creditors.");
    }

    private static RequestValidation? ValidateRequest(
        GameState gameState,
        PaymentObligation obligation,
        LiquidationExecutionContext context)
    {
        if (!Enum.IsDefined(context))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment liquidation context is not supported.");
        }

        var obligationValidation = ValidateObligation(obligation);
        if (obligationValidation is not null)
        {
            return obligationValidation;
        }

        if (gameState.Status != GameStatus.InProgress || gameState.Phase == GamePhase.Completed)
        {
            return new RequestValidation(LiquidationExecutionResultKind.GameNotInProgress, "Game is not in progress.");
        }

        if (gameState.ActiveAuctionState is not null &&
            !IsPermittedAuctionPayment(gameState.ActiveAuctionState, obligation, context))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.ActiveAuction,
                "Payment liquidation is blocked during active auctions.");
        }

        if (HasUnresolvedTileExecution(gameState) && context != LiquidationExecutionContext.TileExecutionPayment)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.UnresolvedTileExecution,
                "Payment liquidation is blocked while the current tile is awaiting execution.");
        }

        var debtor = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);
        if (debtor is null)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorNotInGame,
                "Payment obligation debtor is not in the game.");
        }

        if (debtor.IsBankrupt)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorBankrupt,
                "Bankrupt players cannot satisfy payment obligations.");
        }

        if (debtor.IsEliminated)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorEliminated,
                "Eliminated players cannot satisfy payment obligations.");
        }

        if (obligation.Creditor.Kind == PaymentObligationCreditorKind.Player)
        {
            if (obligation.Creditor.PlayerId is not { } creditorPlayerId)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.InvalidObligation,
                    "Player creditor obligations must include a creditor player.");
            }

            var creditor = PropertyRuleHelpers.FindPlayer(gameState.Players, creditorPlayerId);
            if (creditor is null)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.CreditorNotInGame,
                    "Payment obligation creditor is not in the game.");
            }

            if (creditor.IsBankrupt)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.CreditorBankrupt,
                    "Bankrupt players cannot receive payment obligations.");
            }

            if (creditor.IsEliminated)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.CreditorEliminated,
                    "Eliminated players cannot receive payment obligations.");
            }
        }

        return null;
    }

    private static RequestValidation? ValidateRequest(
        GameState gameState,
        MultiCreditorPaymentObligation obligation,
        LiquidationExecutionContext context)
    {
        if (!Enum.IsDefined(context))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment liquidation context is not supported.");
        }

        if (obligation.AmountPerCreditor.Amount <= 0)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment obligation amount must be positive.");
        }

        if (!Enum.IsDefined(obligation.Kind))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment obligation kind is not supported.");
        }

        if (gameState.Status != GameStatus.InProgress || gameState.Phase == GamePhase.Completed)
        {
            return new RequestValidation(LiquidationExecutionResultKind.GameNotInProgress, "Game is not in progress.");
        }

        if (gameState.ActiveAuctionState is not null)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.ActiveAuction,
                "Payment liquidation is blocked during active auctions.");
        }

        if (HasUnresolvedTileExecution(gameState) && context != LiquidationExecutionContext.TileExecutionPayment)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.UnresolvedTileExecution,
                "Payment liquidation is blocked while the current tile is awaiting execution.");
        }

        var debtor = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);
        if (debtor is null)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorNotInGame,
                "Payment obligation debtor is not in the game.");
        }

        if (debtor.IsBankrupt)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorBankrupt,
                "Bankrupt players cannot satisfy payment obligations.");
        }

        if (debtor.IsEliminated)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.DebtorEliminated,
                "Eliminated players cannot satisfy payment obligations.");
        }

        return null;
    }

    private static RequestValidation? ValidateObligation(PaymentObligation obligation)
    {
        if (obligation.Amount.Amount <= 0)
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment obligation amount must be positive.");
        }

        if (!Enum.IsDefined(obligation.Kind))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment obligation kind is not supported.");
        }

        if (!Enum.IsDefined(obligation.Creditor.Kind))
        {
            return new RequestValidation(
                LiquidationExecutionResultKind.InvalidObligation,
                "Payment obligation creditor kind is not supported.");
        }

        if (obligation.Creditor.Kind == PaymentObligationCreditorKind.Player)
        {
            if (obligation.Creditor.PlayerId is null)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.InvalidObligation,
                    "Player creditor obligations must include a creditor player.");
            }

            if (obligation.Creditor.PlayerId == obligation.DebtorPlayerId)
            {
                return new RequestValidation(
                    LiquidationExecutionResultKind.UnsupportedPayment,
                    "Payment obligation debtor and creditor must be distinct players.");
            }
        }

        return null;
    }

    private static GameState CreateAssetLiquidationState(
        GameState gameState,
        LiquidationExecutionContext context)
    {
        var assetLiquidationState = context == LiquidationExecutionContext.AuctionPayment
            ? gameState with { ActiveAuctionState = null }
            : gameState;

        return context == LiquidationExecutionContext.TileExecutionPayment && HasUnresolvedTileExecution(gameState)
            ? assetLiquidationState with { HasExecutedTileThisTurn = true }
            : assetLiquidationState;
    }

    private static GameState RestoreTileExecutionState(
        GameState updatedState,
        GameState previousState,
        LiquidationExecutionContext context)
    {
        var restoredState = context == LiquidationExecutionContext.AuctionPayment
            ? updatedState with { ActiveAuctionState = previousState.ActiveAuctionState }
            : updatedState;

        return context == LiquidationExecutionContext.TileExecutionPayment && HasUnresolvedTileExecution(previousState)
            ? restoredState with
            {
                CurrentTurnPlayerId = previousState.CurrentTurnPlayerId,
                HasRolledThisTurn = previousState.HasRolledThisTurn,
                HasResolvedTileThisTurn = previousState.HasResolvedTileThisTurn,
                HasExecutedTileThisTurn = previousState.HasExecutedTileThisTurn,
            }
            : restoredState;
    }

    private static Tile? FindNextUpgradeSaleCandidate(GameState gameState, Player debtor)
    {
        return gameState.Board.Tiles
            .Where(tile => IsLegalUpgradeSaleCandidate(gameState, debtor, tile))
            .OrderBy(tile => tile.Index)
            .ThenBy(tile => tile.TileId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsLegalUpgradeSaleCandidate(GameState gameState, Player debtor, Tile tile)
    {
        if (!gameState.Rules.Economy.UpgradesEnabled ||
            !debtor.OwnedPropertyIds.Contains(tile.TileId) ||
            !PropertyRuleHelpers.IsBuildableProperty(tile) ||
            PropertyRuleHelpers.GetPropertyStateData(gameState, tile.TileId).UpgradeLevel <= 0)
        {
            return false;
        }

        var groupTiles = PropertyRuleHelpers.GetBuildableGroupTiles(gameState.Board, tile);
        if (groupTiles.Count < 2 ||
            groupTiles.Any(groupTile => !debtor.OwnedPropertyIds.Contains(groupTile.TileId)) ||
            groupTiles.Any(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).IsMortgaged))
        {
            return false;
        }

        var currentUpgradeLevel = PropertyRuleHelpers.GetPropertyStateData(gameState, tile.TileId).UpgradeLevel;
        var groupMaxUpgradeLevel = groupTiles
            .Select(groupTile => PropertyRuleHelpers.GetPropertyStateData(gameState, groupTile.TileId).UpgradeLevel)
            .Max();
        return currentUpgradeLevel == groupMaxUpgradeLevel;
    }

    private static Tile? FindNextMortgageCandidate(GameState gameState, Player debtor)
    {
        if (!gameState.Rules.Economy.MortgagesEnabled)
        {
            return null;
        }

        return gameState.Board.Tiles
            .Where(tile => debtor.OwnedPropertyIds.Contains(tile.TileId))
            .Where(tile => tile.IsPurchasable && tile.Price is not null)
            .Where(tile =>
            {
                var data = PropertyRuleHelpers.GetPropertyStateData(gameState, tile.TileId);
                return data.UpgradeLevel == 0 && !data.IsMortgaged;
            })
            .OrderBy(tile => tile.Index)
            .ThenBy(tile => tile.TileId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static LiquidationExecutionResult Rejected(
        LiquidationExecutionResultKind kind,
        GameState gameState,
        PaymentObligation obligation,
        string message)
    {
        var debtor = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);
        var creditorBalance = obligation.Creditor.Kind == PaymentObligationCreditorKind.Player &&
            obligation.Creditor.PlayerId is not null
                ? PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.Creditor.PlayerId.Value)?.Money
                : null;

        return new LiquidationExecutionResult(
            kind,
            gameState,
            obligation,
            Array.Empty<LiquidationStepResult>(),
            Money.Zero,
            debtor?.Money ?? Money.Zero,
            creditorBalance,
            message);
    }

    private static MultiCreditorLiquidationExecutionResult Rejected(
        LiquidationExecutionResultKind kind,
        GameState gameState,
        MultiCreditorPaymentObligation obligation,
        IReadOnlyList<PlayerId> creditorPlayerIds,
        Money amountDue,
        string message)
    {
        var debtor = PropertyRuleHelpers.FindPlayer(gameState.Players, obligation.DebtorPlayerId);

        return new MultiCreditorLiquidationExecutionResult(
            kind,
            gameState,
            obligation,
            creditorPlayerIds,
            Array.Empty<LiquidationStepResult>(),
            amountDue,
            Money.Zero,
            debtor?.Money ?? Money.Zero,
            GetCreditorBalances(gameState, creditorPlayerIds),
            message);
    }

    private static bool IsPermittedAuctionPayment(
        AuctionState auctionState,
        PaymentObligation obligation,
        LiquidationExecutionContext context)
    {
        return context == LiquidationExecutionContext.AuctionPayment &&
            obligation.Kind == PaymentObligationKind.AuctionPayment &&
            obligation.Creditor.Kind == PaymentObligationCreditorKind.Bank &&
            obligation.TileId == auctionState.PropertyTileId &&
            auctionState.Bids.Any(bid =>
                bid.BidderId == obligation.DebtorPlayerId &&
                bid.Amount == obligation.Amount) &&
            auctionState.Status is AuctionStatus.AwaitingInitialBid or AuctionStatus.ActiveBidCountdown;
    }

    private static IReadOnlyList<PlayerId> GetActiveCreditorPlayerIds(GameState gameState, PlayerId debtorPlayerId)
    {
        return gameState.Players
            .Where(player => player.PlayerId != debtorPlayerId)
            .Where(player => !player.IsBankrupt && !player.IsEliminated)
            .Select(player => player.PlayerId)
            .ToArray();
    }

    private static Money? CalculateTotalDue(Money amountPerCreditor, int creditorCount)
    {
        if (creditorCount == 0)
        {
            return Money.Zero;
        }

        return amountPerCreditor.Amount > int.MaxValue / creditorCount
            ? null
            : new Money(amountPerCreditor.Amount * creditorCount);
    }

    private static IReadOnlyDictionary<PlayerId, Money> GetCreditorBalances(
        GameState gameState,
        IReadOnlyList<PlayerId> creditorPlayerIds)
    {
        return creditorPlayerIds.ToDictionary(
            creditorPlayerId => creditorPlayerId,
            creditorPlayerId => PropertyRuleHelpers.FindPlayer(gameState.Players, creditorPlayerId)?.Money ?? Money.Zero);
    }

    private static bool HasUnresolvedTileExecution(GameState gameState)
    {
        return gameState.CurrentTurnPlayerId is not null &&
            gameState.HasRolledThisTurn &&
            gameState.HasResolvedTileThisTurn &&
            !gameState.HasExecutedTileThisTurn;
    }

    private static int? FindPlayerIndex(IReadOnlyList<Player> players, PlayerId playerId)
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

    private readonly record struct RequestValidation(LiquidationExecutionResultKind Kind, string Message);
}
