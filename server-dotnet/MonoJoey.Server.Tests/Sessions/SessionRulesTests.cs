namespace MonoJoey.Server.Tests.Sessions;

using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Sessions;
using MonoJoey.Shared.Protocol;
using MonoJoey.Shared.Schemas;

public class SessionRulesTests
{
    [Fact]
    public void CreateSession_InitializesDraftRules()
    {
        var sessionManager = new SessionManager();

        var session = sessionManager.CreateSession();

        Assert.Equal("monojoey_default", session.DraftRules.PresetId);
        Assert.False(session.DraftRules.IsCustom);
        Assert.Equal(9, session.DraftRules.Auction.InitialTimerSeconds);
        Assert.True(session.DraftRules.Jail.Enabled);
        Assert.True(session.DraftRules.Jail.EscapeCardsEnabled);
        Assert.Equal(50, session.DraftRules.Jail.FineAmount);
        Assert.Equal(3, session.DraftRules.Jail.MaxTurns);
        Assert.False(session.DraftRules.Dice.DoublesExtraTurnEnabled);
        Assert.Equal(3, session.DraftRules.Dice.MaxConsecutiveDoublesBeforeLockup);
    }

    [Fact]
    public void StartGame_CopiesDraftRulesIntoGameState()
    {
        var sessionManager = new SessionManager();
        var session = CreateReadyLobby(sessionManager);
        var draftRules = GameRulesPresets.MonoJoeyDefault with
        {
            PresetId = "custom",
            PresetName = "House rules",
            IsCustom = true,
            Auction = GameRulesPresets.MonoJoeyDefault.Auction with
            {
                InitialTimerSeconds = 12,
            },
            Jail = GameRulesPresets.MonoJoeyDefault.Jail with
            {
                EscapeCardsEnabled = false,
                FineAmount = 75,
                MaxTurns = 4,
            },
            Dice = GameRulesPresets.MonoJoeyDefault.Dice with
            {
                DoublesExtraTurnEnabled = true,
                MaxConsecutiveDoublesBeforeLockup = 2,
            },
        };
        _ = sessionManager.SetDraftRules(session.SessionId, new PlayerId("player_1"), draftRules);

        var startedSession = sessionManager.StartGame(session.SessionId);

        AssertRulesEquivalent(draftRules, startedSession.DraftRules);
        AssertRulesEquivalent(draftRules, startedSession.GameState.Rules);
        Assert.NotSame(startedSession.DraftRules, startedSession.GameState.Rules);
        Assert.NotSame(startedSession.DraftRules.Economy, startedSession.GameState.Rules.Economy);
        Assert.NotSame(startedSession.DraftRules.Auction, startedSession.GameState.Rules.Auction);
        Assert.NotSame(startedSession.DraftRules.Jail, startedSession.GameState.Rules.Jail);
        Assert.NotSame(startedSession.DraftRules.Dice, startedSession.GameState.Rules.Dice);
        Assert.NotSame(startedSession.DraftRules.Cards, startedSession.GameState.Rules.Cards);
        Assert.NotSame(startedSession.DraftRules.Loans, startedSession.GameState.Rules.Loans);
        Assert.NotSame(startedSession.DraftRules.Win, startedSession.GameState.Rules.Win);
        Assert.NotSame(startedSession.DraftRules.Future, startedSession.GameState.Rules.Future);
        Assert.Equal(12, startedSession.GameState.Rules.Auction.InitialTimerSeconds);
        Assert.False(startedSession.GameState.Rules.Jail.EscapeCardsEnabled);
        Assert.Equal(75, startedSession.GameState.Rules.Jail.FineAmount);
        Assert.Equal(4, startedSession.GameState.Rules.Jail.MaxTurns);
        Assert.True(startedSession.GameState.Rules.Dice.DoublesExtraTurnEnabled);
        Assert.Equal(2, startedSession.GameState.Rules.Dice.MaxConsecutiveDoublesBeforeLockup);
    }

    [Fact]
    public void GameRulesDeepCopy_DoesNotShareNestedReferencesOrDeckCollections()
    {
        var rules = GameRulesPresets.MonoJoeyDefault;

        var copy = rules.DeepCopy();
        var firstDeckRead = copy.Cards.DecksEnabled;
        var secondDeckRead = copy.Cards.DecksEnabled;

        AssertRulesEquivalent(rules, copy);
        Assert.NotSame(rules, copy);
        Assert.NotSame(rules.Economy, copy.Economy);
        Assert.NotSame(rules.Auction, copy.Auction);
        Assert.NotSame(rules.Jail, copy.Jail);
        Assert.NotSame(rules.Dice, copy.Dice);
        Assert.NotSame(rules.Cards, copy.Cards);
        Assert.NotSame(rules.Loans, copy.Loans);
        Assert.NotSame(rules.Win, copy.Win);
        Assert.NotSame(rules.Future, copy.Future);
        Assert.NotSame(firstDeckRead, secondDeckRead);
        Assert.True(copy.Jail.Enabled);
        Assert.True(copy.Jail.EscapeCardsEnabled);
        Assert.Equal(50, copy.Jail.FineAmount);
        Assert.Equal(3, copy.Jail.MaxTurns);

        var mutableDeckRead = Assert.IsType<string[]>(firstDeckRead);
        mutableDeckRead[0] = "mutated";

        Assert.Equal(new[] { "chance", "table" }, copy.Cards.DecksEnabled);
    }

    [Fact]
    public void ChangingLobbyDraftRules_DoesNotMutateStartedGameStateRules()
    {
        var sessionManager = new SessionManager();
        var firstLobby = CreateReadyLobby(sessionManager);
        var startedSession = sessionManager.StartGame(firstLobby.SessionId);
        var secondLobby = CreateReadyLobby(sessionManager);
        var changedRules = GameRulesPresets.MonoJoeyDefault with
        {
            PresetId = "custom",
            PresetName = "Later rules",
            IsCustom = true,
            Auction = GameRulesPresets.MonoJoeyDefault.Auction with
            {
                InitialTimerSeconds = 15,
            },
        };

        _ = sessionManager.SetDraftRules(secondLobby.SessionId, new PlayerId("player_1"), changedRules);

        Assert.Equal(9, startedSession.GameState.Rules.Auction.InitialTimerSeconds);
        Assert.Equal(9, sessionManager.GetSession(firstLobby.SessionId)?.GameState.Rules.Auction.InitialTimerSeconds);
        Assert.Equal(15, sessionManager.GetSession(secondLobby.SessionId)?.DraftRules.Auction.InitialTimerSeconds);
    }

    [Fact]
    public void SetDraftRules_RejectsStartedSession()
    {
        var sessionManager = new SessionManager();
        var startedSession = sessionManager.StartGame(CreateReadyLobby(sessionManager).SessionId);

        var exception = Assert.Throws<InvalidOperationException>(
            () => sessionManager.SetDraftRules(
                startedSession.SessionId,
                new PlayerId("player_1"),
                GameRulesPresets.MonoJoeyDefault));

        Assert.Equal("Session is not in lobby status.", exception.Message);
    }

    private static GameSession CreateReadyLobby(SessionManager sessionManager)
    {
        var session = sessionManager.CreateSession();
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_1", isReady: true));
        _ = sessionManager.JoinSession(session.SessionId, CreatePlayerConnection("player_2", isReady: true));

        return sessionManager.GetSession(session.SessionId)!;
    }

    private static PlayerConnection CreatePlayerConnection(string playerId, bool isReady)
    {
        return new PlayerConnection(
            new PlayerId(playerId),
            $"connection_{playerId}",
            isReady);
    }

    private static void AssertRulesEquivalent(GameRules expected, GameRules actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.PresetId, actual.PresetId);
        Assert.Equal(expected.PresetName, actual.PresetName);
        Assert.Equal(expected.IsCustom, actual.IsCustom);
        Assert.Equal(expected.Economy, actual.Economy);
        Assert.Equal(expected.Auction, actual.Auction);
        Assert.Equal(expected.Jail, actual.Jail);
        Assert.Equal(expected.Dice, actual.Dice);
        Assert.Equal(expected.Cards.DecksEnabled, actual.Cards.DecksEnabled);
        Assert.Equal(expected.Cards.CustomCardsEnabled, actual.Cards.CustomCardsEnabled);
        Assert.Equal(expected.Cards.DeckEditingEnabled, actual.Cards.DeckEditingEnabled);
        Assert.Equal(expected.Loans, actual.Loans);
        Assert.Equal(expected.Win, actual.Win);
        Assert.Equal(expected.Future, actual.Future);
    }
}
