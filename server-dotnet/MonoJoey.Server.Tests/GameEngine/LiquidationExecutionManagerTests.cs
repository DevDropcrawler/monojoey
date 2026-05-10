namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class LiquidationExecutionManagerTests
{
    [Fact]
    public void ExecutePaymentObligation_PaysCashOnlyBankObligation()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 40, PaymentObligationKind.Tax));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(new Money(40), result.AmountPaid);
        Assert.Equal(new Money(60), result.DebtorBalance);
        Assert.Equal(new[] { LiquidationStepKind.BankPayment }, result.Steps.Select(step => step.StepKind));
        Assert.Equal(new Money(60), result.GameState.Players[0].Money);
        Assert.Same(gameState.PropertyStates, result.GameState.PropertyStates);
    }

    [Fact]
    public void ExecutePaymentObligation_PaysCashOnlyPlayerObligation()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 50));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            PlayerObligation("player_1", "player_2", 40));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(new Money(60), result.DebtorBalance);
        Assert.Equal(new Money(90), result.CreditorBalance);
        Assert.Equal(new[] { LiquidationStepKind.PlayerPayment }, result.Steps.Select(step => step.StepKind));
        Assert.Equal(new Money(60), result.GameState.Players[0].Money);
        Assert.Equal(new Money(90), result.GameState.Players[1].Money);
    }

    [Fact]
    public void ExecutePaymentObligation_PaysBankObligationAfterLiquidation()
    {
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(200),
            CurrentInterestRatePercent: 30,
            NextTurnInterestDue: new Money(60),
            LoanTier: 2);
        var gameState = CreateGameState(CreatePlayer("player_1", 10, "property_03") with { LoanState = loanState });

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 60, PaymentObligationKind.CardPayment));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[] { LiquidationStepKind.Mortgage, LiquidationStepKind.BankPayment },
            result.Steps.Select(step => step.StepKind).ToArray());
        Assert.Equal(new TileId("property_03"), result.Steps[0].PropertyTileId);
        Assert.Equal(new Money(50), result.Steps[0].Amount);
        Assert.Equal(Money.Zero, result.DebtorBalance);
        Assert.True(result.GameState.PropertyStates[new TileId("property_03")].Data.IsMortgaged);
        Assert.Same(loanState, result.GameState.Players[0].LoanState);
    }

    [Fact]
    public void ExecutePaymentObligation_PaysPlayerObligationAfterLiquidation()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 10, "property_03"),
            CreatePlayer("player_2", 50));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            PlayerObligation("player_1", "player_2", 60));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[] { LiquidationStepKind.Mortgage, LiquidationStepKind.PlayerPayment },
            result.Steps.Select(step => step.StepKind).ToArray());
        Assert.Equal(Money.Zero, result.DebtorBalance);
        Assert.Equal(new Money(110), result.CreditorBalance);
        Assert.Equal(new Money(110), result.GameState.Players[1].Money);
    }

    [Fact]
    public void ExecutePaymentObligation_SellsUpgradesBeforeMortgaging()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02", "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 55, PaymentObligationKind.Fine));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[]
            {
                (LiquidationStepKind.UpgradeSale, "property_01"),
                (LiquidationStepKind.Mortgage, "property_01"),
                (LiquidationStepKind.BankPayment, (string?)null),
            },
            result.Steps.Select(step => (step.StepKind, step.PropertyTileId?.Value)).ToArray());
        Assert.Equal(Money.Zero, result.DebtorBalance);
        var propertyState = result.GameState.PropertyStates[property01].Data;
        Assert.Equal(0, propertyState.UpgradeLevel);
        Assert.True(propertyState.IsMortgaged);
    }

    [Fact]
    public void ExecutePaymentObligation_OnlySellsCurrentGroupMaxUpgrade()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 2)),
            },
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 25, PaymentObligationKind.Fine));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(LiquidationStepKind.UpgradeSale, result.Steps[0].StepKind);
        Assert.Equal(property02, result.Steps[0].PropertyTileId);
        Assert.Equal(1, result.GameState.PropertyStates[property01].Data.UpgradeLevel);
        Assert.Equal(1, result.GameState.PropertyStates[property02].Data.UpgradeLevel);
    }

    [Fact]
    public void ExecutePaymentObligation_MortgagesAfterAllNeededUpgradesAreSold()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 80, PaymentObligationKind.Fine));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[]
            {
                (LiquidationStepKind.UpgradeSale, "property_01"),
                (LiquidationStepKind.UpgradeSale, "property_02"),
                (LiquidationStepKind.Mortgage, "property_01"),
                (LiquidationStepKind.BankPayment, (string?)null),
            },
            result.Steps.Select(step => (step.StepKind, step.PropertyTileId?.Value)).ToArray());
        Assert.Equal(Money.Zero, result.DebtorBalance);
    }

    [Fact]
    public void ExecutePaymentObligation_PreservesDamageMortgageAndUntouchedUpgradeState()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var property03 = new TileId("property_03");
        var gameState = CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02", "property_03")) with
        {
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(damagePercent: 25, upgradeLevel: 1)),
                [property02] = new(property02, new PropertyStateData(damagePercent: 10)),
                [property03] = new(property03, new PropertyStateData(damagePercent: 40, isMortgaged: true)),
            },
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 55, PaymentObligationKind.Fine));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(25, result.GameState.PropertyStates[property01].Data.DamagePercent);
        Assert.Equal(0, result.GameState.PropertyStates[property01].Data.UpgradeLevel);
        Assert.True(result.GameState.PropertyStates[property01].Data.IsMortgaged);
        Assert.Equal(10, result.GameState.PropertyStates[property02].Data.DamagePercent);
        Assert.Equal(0, result.GameState.PropertyStates[property02].Data.UpgradeLevel);
        Assert.Equal(40, result.GameState.PropertyStates[property03].Data.DamagePercent);
        Assert.True(result.GameState.PropertyStates[property03].Data.IsMortgaged);
    }

    [Fact]
    public void ExecutePaymentObligation_ReturnsOriginalStateWhenAssetsCannotCover()
    {
        var property03 = new TileId("property_03");
        var loanState = new PlayerLoanState(
            TotalBorrowed: new Money(1000),
            CurrentInterestRatePercent: 50,
            NextTurnInterestDue: new Money(500),
            LoanTier: 3);
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData(damagePercent: 15)),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", 5, "property_03") with { LoanState = loanState }) with
        {
            PropertyStates = propertyStates,
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 80, PaymentObligationKind.Fine));

        Assert.Equal(LiquidationExecutionResultKind.Insolvent, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
        Assert.Equal(new Money(5), result.GameState.Players[0].Money);
        Assert.Same(loanState, result.GameState.Players[0].LoanState);
        Assert.False(result.GameState.PropertyStates[property03].Data.IsMortgaged);
    }

    [Theory]
    [InlineData("auction", LiquidationExecutionResultKind.ActiveAuction)]
    [InlineData("unresolved_tile", LiquidationExecutionResultKind.UnresolvedTileExecution)]
    [InlineData("completed_status", LiquidationExecutionResultKind.GameNotInProgress)]
    [InlineData("completed_phase", LiquidationExecutionResultKind.GameNotInProgress)]
    public void ExecutePaymentObligation_RejectsBlockedGameStates(
        string scenario,
        LiquidationExecutionResultKind expectedKind)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100, "property_03"));
        gameState = scenario switch
        {
            "auction" => gameState with { ActiveAuctionState = CreateAuctionState() },
            "unresolved_tile" => gameState with
            {
                HasRolledThisTurn = true,
                HasResolvedTileThisTurn = true,
                HasExecutedTileThisTurn = false,
            },
            "completed_status" => gameState with { Status = GameStatus.Completed },
            "completed_phase" => gameState with { Phase = GamePhase.Completed },
            _ => gameState,
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 40, PaymentObligationKind.Fine),
            LiquidationExecutionContext.Normal);

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void ExecutePaymentObligation_TileExecutionPaymentPaysCashOnlyBankObligation()
    {
        var gameState = CreateUnresolvedTileExecutionState(CreateGameState(CreatePlayer("player_1", 100)));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 40, PaymentObligationKind.Tax),
            LiquidationExecutionContext.TileExecutionPayment);

        Assert.True(result.PaymentExecuted);
        Assert.Equal(new[] { LiquidationStepKind.BankPayment }, result.Steps.Select(step => step.StepKind));
        Assert.Equal(new Money(60), result.DebtorBalance);
        Assert.Equal(new Money(60), result.GameState.Players[0].Money);
        AssertUnresolvedTileExecutionPreserved(result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_TileExecutionPaymentPaysBankObligationAfterUpgradeSaleAndMortgage()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateUnresolvedTileExecutionState(
            CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02", "property_03")) with
            {
                PropertyStates = new Dictionary<TileId, PropertyState>
                {
                    [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
                },
            });

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 55, PaymentObligationKind.CardPayment),
            LiquidationExecutionContext.TileExecutionPayment);

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[]
            {
                (LiquidationStepKind.UpgradeSale, "property_01"),
                (LiquidationStepKind.Mortgage, "property_01"),
                (LiquidationStepKind.BankPayment, (string?)null),
            },
            result.Steps.Select(step => (step.StepKind, step.PropertyTileId?.Value)).ToArray());
        Assert.Equal(Money.Zero, result.DebtorBalance);
        Assert.Equal(0, result.GameState.PropertyStates[property01].Data.UpgradeLevel);
        Assert.True(result.GameState.PropertyStates[property01].Data.IsMortgaged);
        AssertUnresolvedTileExecutionPreserved(result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_TileExecutionPaymentPaysPlayerObligationAfterLiquidation()
    {
        var gameState = CreateUnresolvedTileExecutionState(CreateGameState(
            CreatePlayer("player_1", 10, "property_03"),
            CreatePlayer("player_2", 50)));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            PlayerObligation("player_1", "player_2", 60),
            LiquidationExecutionContext.TileExecutionPayment);

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[] { LiquidationStepKind.Mortgage, LiquidationStepKind.PlayerPayment },
            result.Steps.Select(step => step.StepKind).ToArray());
        Assert.Equal(Money.Zero, result.DebtorBalance);
        Assert.Equal(new Money(110), result.CreditorBalance);
        Assert.Equal(new Money(110), result.GameState.Players[1].Money);
        AssertUnresolvedTileExecutionPreserved(result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_TileExecutionPaymentRejectsActiveAuction()
    {
        var gameState = CreateUnresolvedTileExecutionState(CreateGameState(CreatePlayer("player_1", 100))) with
        {
            ActiveAuctionState = CreateAuctionState(),
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 40, PaymentObligationKind.Tax),
            LiquidationExecutionContext.TileExecutionPayment);

        Assert.Equal(LiquidationExecutionResultKind.ActiveAuction, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void ExecutePaymentObligation_TileExecutionPaymentReturnsOriginalStateWhenAssetsCannotCover()
    {
        var property03 = new TileId("property_03");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData(damagePercent: 15)),
        };
        var gameState = CreateUnresolvedTileExecutionState(CreateGameState(CreatePlayer("player_1", 5, "property_03")) with
        {
            PropertyStates = propertyStates,
        });

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", 80, PaymentObligationKind.Tax),
            LiquidationExecutionContext.TileExecutionPayment);

        Assert.Equal(LiquidationExecutionResultKind.Insolvent, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
        Assert.Equal(new Money(5), result.GameState.Players[0].Money);
        Assert.False(result.GameState.PropertyStates[property03].Data.IsMortgaged);
        AssertUnresolvedTileExecutionPreserved(result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_AuctionPaymentAllowsMatchingActiveAuctionLiquidation()
    {
        var auctionState = CreateAuctionState() with
        {
            PropertyTileId = new TileId("property_01"),
            Bids = new[] { new AuctionBid(new PlayerId("player_1"), new Money(60), DateTimeOffset.Parse("2026-04-26T00:00:01+00:00")) },
            HighestBid = new Money(60),
            HighestBidderId = new PlayerId("player_1"),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", 10, "property_03")) with
        {
            ActiveAuctionState = auctionState,
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            new PaymentObligation(
                new PlayerId("player_1"),
                new Money(60),
                PaymentObligationKind.AuctionPayment,
                PaymentObligationCreditor.Bank,
                new TileId("property_01")),
            LiquidationExecutionContext.AuctionPayment);

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[] { LiquidationStepKind.Mortgage, LiquidationStepKind.BankPayment },
            result.Steps.Select(step => step.StepKind).ToArray());
        Assert.Same(auctionState, result.GameState.ActiveAuctionState);
        Assert.True(result.GameState.PropertyStates[new TileId("property_03")].Data.IsMortgaged);
    }

    [Fact]
    public void ExecutePaymentObligation_AuctionPaymentRejectsMismatchedActiveAuction()
    {
        var auctionState = CreateAuctionState() with
        {
            PropertyTileId = new TileId("property_01"),
            Bids = new[] { new AuctionBid(new PlayerId("player_2"), new Money(60), DateTimeOffset.Parse("2026-04-26T00:00:01+00:00")) },
            HighestBid = new Money(60),
            HighestBidderId = new PlayerId("player_2"),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", 100)) with
        {
            ActiveAuctionState = auctionState,
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            new PaymentObligation(
                new PlayerId("player_1"),
                new Money(60),
                PaymentObligationKind.AuctionPayment,
                PaymentObligationCreditor.Bank,
                new TileId("property_01")),
            LiquidationExecutionContext.AuctionPayment);

        Assert.Equal(LiquidationExecutionResultKind.ActiveAuction, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_AuctionPaymentSellsUpgradesBeforeMortgagesInTileOrder()
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var auctionState = CreateAuctionState() with
        {
            PropertyTileId = new TileId("property_03"),
            Bids = new[] { new AuctionBid(new PlayerId("player_1"), new Money(80), DateTimeOffset.Parse("2026-04-26T00:00:01+00:00")) },
            HighestBid = new Money(80),
            HighestBidderId = new PlayerId("player_1"),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", 0, "property_01", "property_02")) with
        {
            ActiveAuctionState = auctionState,
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
                [property02] = new(property02, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            new PaymentObligation(
                new PlayerId("player_1"),
                new Money(80),
                PaymentObligationKind.AuctionPayment,
                PaymentObligationCreditor.Bank,
                new TileId("property_03")),
            LiquidationExecutionContext.AuctionPayment);

        Assert.True(result.PaymentExecuted);
        Assert.Equal(
            new[]
            {
                (LiquidationStepKind.UpgradeSale, "property_01"),
                (LiquidationStepKind.UpgradeSale, "property_02"),
                (LiquidationStepKind.Mortgage, "property_01"),
                (LiquidationStepKind.BankPayment, (string?)null),
            },
            result.Steps.Select(step => (step.StepKind, step.PropertyTileId?.Value)).ToArray());
    }

    [Fact]
    public void ExecuteMultiCreditorPaymentObligation_PaysActiveCreditorsInPlayerOrder()
    {
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_3", 50),
            CreatePlayer("player_2", 60),
            CreatePlayer("player_4", 70) with { IsEliminated = true });

        var result = LiquidationExecutionManager.ExecuteMultiCreditorPaymentObligation(
            gameState,
            new MultiCreditorPaymentObligation(
                new PlayerId("player_1"),
                new Money(10),
                PaymentObligationKind.CardPayment,
                CardId: new CardId("card_pay_all")));

        Assert.True(result.PaymentExecuted);
        Assert.Equal(new[] { "player_3", "player_2" }, result.CreditorPlayerIds.Select(playerId => playerId.Value).ToArray());
        Assert.Equal(new[] { "player_3", "player_2" }, result.Steps.Select(step => step.CreditorPlayerId?.Value).ToArray());
        Assert.Equal(new Money(80), result.GameState.Players[0].Money);
        Assert.Equal(new Money(60), result.GameState.Players[1].Money);
        Assert.Equal(new Money(70), result.GameState.Players[2].Money);
        Assert.Equal(new Money(70), result.GameState.Players[3].Money);
    }

    [Fact]
    public void ExecuteMultiCreditorPaymentObligation_ReturnsOriginalStateWhenAssetsCannotCover()
    {
        var property03 = new TileId("property_03");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData()),
        };
        var gameState = CreateGameState(
            CreatePlayer("player_1", 5, "property_03"),
            CreatePlayer("player_2", 50),
            CreatePlayer("player_3", 60)) with
        {
            PropertyStates = propertyStates,
        };

        var result = LiquidationExecutionManager.ExecuteMultiCreditorPaymentObligation(
            gameState,
            new MultiCreditorPaymentObligation(
                new PlayerId("player_1"),
                new Money(30),
                PaymentObligationKind.CardPayment,
                CardId: new CardId("card_pay_all")));

        Assert.Equal(LiquidationExecutionResultKind.Insolvent, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
        Assert.Equal(new Money(5), result.GameState.Players[0].Money);
        Assert.Equal(new Money(50), result.GameState.Players[1].Money);
        Assert.Equal(new Money(60), result.GameState.Players[2].Money);
        Assert.False(result.GameState.PropertyStates[property03].Data.IsMortgaged);
    }

    [Theory]
    [InlineData("missing_debtor", LiquidationExecutionResultKind.DebtorNotInGame)]
    [InlineData("bankrupt_debtor", LiquidationExecutionResultKind.DebtorBankrupt)]
    [InlineData("eliminated_debtor", LiquidationExecutionResultKind.DebtorEliminated)]
    [InlineData("missing_creditor", LiquidationExecutionResultKind.CreditorNotInGame)]
    [InlineData("bankrupt_creditor", LiquidationExecutionResultKind.CreditorBankrupt)]
    [InlineData("eliminated_creditor", LiquidationExecutionResultKind.CreditorEliminated)]
    public void ExecutePaymentObligation_RejectsInvalidDebtorAndCreditorStates(
        string scenario,
        LiquidationExecutionResultKind expectedKind)
    {
        var debtorId = new PlayerId("player_1");
        var creditorId = new PlayerId("player_2");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 50));
        gameState = scenario switch
        {
            "bankrupt_debtor" => gameState with { Players = SetPlayer(gameState.Players, debtorId, player => player with { IsBankrupt = true }) },
            "eliminated_debtor" => gameState with { Players = SetPlayer(gameState.Players, debtorId, player => player with { IsEliminated = true }) },
            "bankrupt_creditor" => gameState with { Players = SetPlayer(gameState.Players, creditorId, player => player with { IsBankrupt = true }) },
            "eliminated_creditor" => gameState with { Players = SetPlayer(gameState.Players, creditorId, player => player with { IsEliminated = true }) },
            _ => gameState,
        };
        var obligation = scenario switch
        {
            "missing_debtor" => PlayerObligation("missing_player", "player_2", 40),
            "missing_creditor" => PlayerObligation("player_1", "missing_player", 40),
            _ => PlayerObligation("player_1", "player_2", 40),
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(gameState, obligation);

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Empty(result.Steps);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExecutePaymentObligation_RejectsInvalidAmounts(int amount)
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100));

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            BankObligation("player_1", amount, PaymentObligationKind.Fine));

        Assert.Equal(LiquidationExecutionResultKind.InvalidObligation, result.ResultKind);
        Assert.Same(gameState, result.GameState);
    }

    [Fact]
    public void ExecutePaymentObligation_ReturnsOriginalStateWhenPlayerPaymentOverflows()
    {
        var property03 = new TileId("property_03");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData()),
        };
        var gameState = CreateGameState(
            CreatePlayer("player_1", 10, "property_03"),
            CreatePlayer("player_2", int.MaxValue - 20)) with
        {
            PropertyStates = propertyStates,
        };

        var result = LiquidationExecutionManager.ExecutePaymentObligation(
            gameState,
            PlayerObligation("player_1", "player_2", 40));

        Assert.Equal(LiquidationExecutionResultKind.UnsafeMoneyBalance, result.ResultKind);
        Assert.Same(gameState, result.GameState);
        Assert.Same(propertyStates, result.GameState.PropertyStates);
        Assert.False(result.GameState.PropertyStates[property03].Data.IsMortgaged);
        Assert.Equal(new Money(10), result.GameState.Players[0].Money);
    }

    private static PaymentObligation BankObligation(string playerId, int amount, PaymentObligationKind kind)
    {
        return new PaymentObligation(
            new PlayerId(playerId),
            new Money(amount),
            kind,
            PaymentObligationCreditor.Bank);
    }

    private static PaymentObligation PlayerObligation(string debtorId, string creditorId, int amount)
    {
        return new PaymentObligation(
            new PlayerId(debtorId),
            new Money(amount),
            PaymentObligationKind.Rent,
            PaymentObligationCreditor.ForPlayer(new PlayerId(creditorId)),
            new TileId("property_01"));
    }

    private static GameState CreateGameState(params Player[] players)
    {
        return new GameState(
            new MatchId("match_123"),
            GamePhase.AwaitingRoll,
            DefaultBoardFactory.Create(),
            players,
            players[0].PlayerId,
            TurnNumber: 1,
            DateTimeOffset.Parse("2026-04-26T00:00:00+00:00"),
            EndedAtUtc: null)
        {
            Rules = UpgradeRules(),
        };
    }

    private static GameState CreateUnresolvedTileExecutionState(GameState gameState)
    {
        return gameState with
        {
            HasRolledThisTurn = true,
            HasResolvedTileThisTurn = true,
            HasExecutedTileThisTurn = false,
        };
    }

    private static void AssertUnresolvedTileExecutionPreserved(GameState gameState)
    {
        Assert.True(gameState.HasRolledThisTurn);
        Assert.True(gameState.HasResolvedTileThisTurn);
        Assert.False(gameState.HasExecutedTileThisTurn);
    }

    private static Player CreatePlayer(
        string playerId,
        int money,
        params string[] ownedPropertyIds)
    {
        return new Player(
            new PlayerId(playerId),
            playerId,
            $"token_{playerId}",
            $"color_{playerId}",
            new Money(money),
            new TileId("start"),
            ownedPropertyIds.Select(propertyId => new TileId(propertyId)).ToHashSet(),
            new HashSet<CardId>(),
            IsBankrupt: false,
            IsEliminated: false);
    }

    private static IReadOnlyList<Player> SetPlayer(
        IReadOnlyList<Player> players,
        PlayerId playerId,
        Func<Player, Player> update)
    {
        return players
            .Select(player => player.PlayerId == playerId ? update(player) : player)
            .ToArray();
    }

    private static GameRules UpgradeRules()
    {
        return GameRulesPresets.MonoJoeyDefault with
        {
            Economy = GameRulesPresets.MonoJoeyDefault.Economy with
            {
                UpgradesEnabled = true,
            },
        };
    }

    private static AuctionState CreateAuctionState()
    {
        return new AuctionState(
            new TileId("property_01"),
            new PlayerId("player_1"),
            AuctionStatus.AwaitingInitialBid,
            Money.Zero,
            new Money(1),
            InitialPreBidSeconds: 9,
            BidResetSeconds: 3,
            Array.Empty<AuctionBid>(),
            HighestBid: null,
            HighestBidderId: null,
            CountdownDurationSeconds: 9,
            TimerEndsAtUtc: DateTimeOffset.Parse("2026-04-26T00:00:09+00:00"));
    }
}
