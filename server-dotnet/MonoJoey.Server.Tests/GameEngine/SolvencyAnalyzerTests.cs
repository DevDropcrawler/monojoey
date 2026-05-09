namespace MonoJoey.Server.Tests.GameEngine;

using MonoJoey.Server.GameEngine;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class SolvencyAnalyzerTests
{
    [Fact]
    public void PaymentObligationKind_ContainsOnlyImplementedPaymentObligations()
    {
        var names = Enum.GetNames<PaymentObligationKind>();

        Assert.Equal(
            new[]
            {
                "Rent",
                "Tax",
                "CardPayment",
                "AuctionPayment",
                "Fine",
                "LoanInterest",
            },
            names);
    }

    [Fact]
    public void Analyze_ReturnsSolventWithCashForBankObligation()
    {
        var obligation = BankObligation("player_1", 100, PaymentObligationKind.Tax);
        var gameState = CreateGameState(CreatePlayer("player_1", 100));

        var result = SolvencyAnalyzer.Analyze(gameState, obligation);

        Assert.Equal(SolvencyAnalysisResultKind.SolventWithCash, result.ResultKind);
        Assert.Equal(PaymentObligationCreditor.Bank, result.Obligation.Creditor);
        Assert.Equal(new Money(100), result.CashOnHand);
        Assert.Equal(Money.Zero, result.Shortfall);
        Assert.Empty(result.RaiseableCashItems);
    }

    [Fact]
    public void Analyze_PreservesPlayerCreditorMetadata()
    {
        var creditorId = new PlayerId("player_2");
        var obligation = new PaymentObligation(
            new PlayerId("player_1"),
            new Money(100),
            PaymentObligationKind.Rent,
            PaymentObligationCreditor.ForPlayer(creditorId),
            new TileId("property_01"));
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100),
            CreatePlayer("player_2", 1500, "property_01"));

        var result = SolvencyAnalyzer.Analyze(gameState, obligation);

        Assert.Equal(SolvencyAnalysisResultKind.SolventWithCash, result.ResultKind);
        Assert.Equal(PaymentObligationCreditorKind.Player, result.Obligation.Creditor.Kind);
        Assert.Equal(creditorId, result.Obligation.Creditor.PlayerId);
    }

    [Fact]
    public void Analyze_ReturnsSolventWithAssetsWhenMortgageCanCoverShortfall()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 10, "property_03"));

        var result = SolvencyAnalyzer.Analyze(gameState, BankObligation("player_1", 60, PaymentObligationKind.AuctionPayment));

        Assert.Equal(SolvencyAnalysisResultKind.SolventWithAssets, result.ResultKind);
        Assert.Equal(new Money(10), result.CashOnHand);
        Assert.Equal(new Money(50), result.RaiseableCash.TotalRaiseableCash);
        Assert.Equal(new Money(60), result.TotalAvailable);
        Assert.Equal(Money.Zero, result.Shortfall);
        var item = Assert.Single(result.RaiseableCashItems);
        Assert.Equal(RaiseableCashSource.Mortgage, item.Source);
        Assert.Equal(new TileId("property_03"), item.PropertyTileId);
        Assert.Equal(new Money(50), item.Amount);
    }

    [Fact]
    public void Analyze_ReturnsInsolventWithShortfallWhenCashAndAssetsCannotCover()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 5, "property_03"));

        var result = SolvencyAnalyzer.Analyze(gameState, BankObligation("player_1", 80, PaymentObligationKind.CardPayment));

        Assert.Equal(SolvencyAnalysisResultKind.Insolvent, result.ResultKind);
        Assert.Equal(new Money(55), result.TotalAvailable);
        Assert.Equal(new Money(25), result.Shortfall);
    }

    [Theory]
    [InlineData("missing", SolvencyAnalysisResultKind.PlayerNotInGame)]
    [InlineData("bankrupt", SolvencyAnalysisResultKind.PlayerBankrupt)]
    [InlineData("eliminated", SolvencyAnalysisResultKind.PlayerEliminated)]
    public void Analyze_RejectsInvalidDebtorsWithoutMutation(string scenario, SolvencyAnalysisResultKind expectedKind)
    {
        var playerId = new PlayerId("player_1");
        var gameState = CreateGameState(CreatePlayer("player_1", 100, "property_03"));
        gameState = scenario switch
        {
            "bankrupt" => gameState with { Players = new[] { gameState.Players[0] with { IsBankrupt = true } } },
            "eliminated" => gameState with { Players = new[] { gameState.Players[0] with { IsEliminated = true } } },
            _ => gameState,
        };
        if (scenario == "missing")
        {
            playerId = new PlayerId("missing_player");
        }
        var originalPlayers = gameState.Players;
        var originalPropertyStates = gameState.PropertyStates;

        var result = SolvencyAnalyzer.Analyze(
            gameState,
            new PaymentObligation(playerId, new Money(20), PaymentObligationKind.Fine, PaymentObligationCreditor.Bank));

        Assert.Equal(expectedKind, result.ResultKind);
        Assert.Same(originalPlayers, gameState.Players);
        Assert.Same(originalPropertyStates, gameState.PropertyStates);
    }

    [Fact]
    public void Analyze_RejectsNegativeObligationAmount()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100));

        var result = SolvencyAnalyzer.Analyze(gameState, BankObligation("player_1", -1, PaymentObligationKind.Fine));

        Assert.Equal(SolvencyAnalysisResultKind.InvalidObligation, result.ResultKind);
    }

    [Fact]
    public void CalculateRaiseableCash_ExcludesAlreadyMortgagedUnownedUnpricedAndNonPurchasableTiles()
    {
        var property01 = new TileId("property_01");
        var board = DefaultBoardFactory.Create();
        var tiles = board.Tiles
            .Select(tile => tile.TileId == new TileId("property_03")
                ? tile with { Price = null }
                : tile)
            .ToArray();
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_01", "property_03", "free_space_01"),
            CreatePlayer("player_2", 100, "property_02")) with
        {
            Board = board with { Tiles = tiles },
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(isMortgaged: true)),
            },
        };

        var result = SolvencyAnalyzer.CalculateRaiseableCash(gameState, new PlayerId("player_1"));

        Assert.Equal(RaiseableCashResultKind.Calculated, result.ResultKind);
        Assert.Equal(Money.Zero, result.TotalRaiseableCash);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void CalculateRaiseableCash_UsesConfiguredMortgagePercent()
    {
        var gameState = CreateGameState(CreatePlayer("player_1", 100, "property_03")) with
        {
            Rules = GameRulesPresets.MonoJoeyDefault with
            {
                Economy = GameRulesPresets.MonoJoeyDefault.Economy with { MortgageValuePercent = 40 },
            },
        };

        var result = SolvencyAnalyzer.CalculateRaiseableCash(gameState, new PlayerId("player_1"));

        var item = Assert.Single(result.Items);
        Assert.Equal(new Money(40), item.Amount);
        Assert.Equal(new Money(40), result.TotalRaiseableCash);
    }

    [Fact]
    public void CalculateRaiseableCash_IncludesUpgradeSaleValueAndMortgageAfterUpgradeLiquidation()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_01", "property_02")) with
        {
            Rules = UpgradeRules(upgradeSellRefundPercent: 25),
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 2)),
            },
        };

        var result = SolvencyAnalyzer.CalculateRaiseableCash(gameState, new PlayerId("player_1"));

        Assert.Equal(new Money(84), result.TotalRaiseableCash);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(new[]
        {
            RaiseableCashSource.UpgradeSale,
            RaiseableCashSource.Mortgage,
            RaiseableCashSource.Mortgage,
        }, result.Items.Select(item => item.Source).ToArray());
        Assert.Equal(new Money(24), result.Items[0].Amount);
        Assert.Equal(2, result.Items[0].UnitCount);
        Assert.False(result.Items[0].RequiresUpgradeLiquidation);
        Assert.Equal(new Money(30), result.Items[1].Amount);
        Assert.True(result.Items[1].RequiresUpgradeLiquidation);
    }

    [Theory]
    [InlineData("disabled_upgrades")]
    [InlineData("incomplete_group")]
    [InlineData("mortgaged_group")]
    public void CalculateRaiseableCash_ExcludesUpgradeValueWhenSaleIsNotAvailable(string scenario)
    {
        var property01 = new TileId("property_01");
        var property02 = new TileId("property_02");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_01", scenario == "incomplete_group" ? "property_03" : "property_02")) with
        {
            Rules = scenario == "disabled_upgrades"
                ? GameRulesPresets.MonoJoeyDefault
                : UpgradeRules(),
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
            },
        };
        if (scenario == "mortgaged_group")
        {
            gameState = gameState with
            {
                PropertyStates = new Dictionary<TileId, PropertyState>(gameState.PropertyStates)
                {
                    [property02] = new(property02, new PropertyStateData(isMortgaged: true)),
                },
            };
        }

        var result = SolvencyAnalyzer.CalculateRaiseableCash(gameState, new PlayerId("player_1"));

        Assert.DoesNotContain(result.Items, item => item.Source == RaiseableCashSource.UpgradeSale);
        Assert.DoesNotContain(result.Items, item => item.PropertyTileId == property01 && item.Source == RaiseableCashSource.Mortgage);
    }

    [Fact]
    public void CalculateRaiseableCash_ReturnsDeterministicItemOrder()
    {
        var property01 = new TileId("property_01");
        var gameState = CreateGameState(
            CreatePlayer("player_1", 100, "property_03", "property_02", "property_01")) with
        {
            Rules = UpgradeRules(),
            PropertyStates = new Dictionary<TileId, PropertyState>
            {
                [property01] = new(property01, new PropertyStateData(upgradeLevel: 1)),
            },
        };

        var result = SolvencyAnalyzer.CalculateRaiseableCash(gameState, new PlayerId("player_1"));

        Assert.Equal(
            new[]
            {
                (RaiseableCashSource.UpgradeSale, "property_01"),
                (RaiseableCashSource.Mortgage, "property_01"),
                (RaiseableCashSource.Mortgage, "property_02"),
                (RaiseableCashSource.Mortgage, "property_03"),
            },
            result.Items.Select(item => (item.Source, item.PropertyTileId.Value)).ToArray());
    }

    [Fact]
    public void Analyze_DoesNotMutateGameStateCollections()
    {
        var property03 = new TileId("property_03");
        var propertyStates = new Dictionary<TileId, PropertyState>
        {
            [property03] = new(property03, new PropertyStateData(35)),
        };
        var gameState = CreateGameState(CreatePlayer("player_1", 5, "property_03")) with
        {
            PropertyStates = propertyStates,
        };
        var players = gameState.Players;
        var ownedPropertyIds = gameState.Players[0].OwnedPropertyIds;

        _ = SolvencyAnalyzer.Analyze(gameState, BankObligation("player_1", 55, PaymentObligationKind.LoanInterest));

        Assert.Same(players, gameState.Players);
        Assert.Same(ownedPropertyIds, gameState.Players[0].OwnedPropertyIds);
        Assert.Same(propertyStates, gameState.PropertyStates);
        Assert.False(gameState.PropertyStates[property03].Data.IsMortgaged);
        Assert.Equal(new Money(5), gameState.Players[0].Money);
    }

    private static PaymentObligation BankObligation(string playerId, int amount, PaymentObligationKind kind)
    {
        return new PaymentObligation(
            new PlayerId(playerId),
            new Money(amount),
            kind,
            PaymentObligationCreditor.Bank);
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
            EndedAtUtc: null);
    }

    private static GameRules UpgradeRules(int upgradeSellRefundPercent = 50)
    {
        return GameRulesPresets.MonoJoeyDefault with
        {
            Economy = GameRulesPresets.MonoJoeyDefault.Economy with
            {
                UpgradesEnabled = true,
                UpgradeSellRefundPercent = upgradeSellRefundPercent,
            },
        };
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
}
