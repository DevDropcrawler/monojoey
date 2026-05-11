using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MonoJoeyMockTransport : MonoBehaviour, IMonoJoeyTransport
{
    private static readonly HashSet<string> GameplayMutationTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "roll_dice",
        "place_bid",
        "execute_tile",
        "resolve_tile",
        "end_turn",
        "finalize_auction",
        "take_loan",
        "mortgage_property",
        "unmortgage_property",
        "upgrade_property",
        "create_trade_offer",
        "accept_trade_offer",
        "decline_trade_offer",
        "cancel_trade_offer"
    };

    private readonly List<string> sentMessages = new List<string>();
    private readonly List<string> sentRequestTypes = new List<string>();

    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> MessageReceived;
    public event Action<string> ErrorReceived;

    public MonoJoeyTransportConnectionState State { get; private set; } = MonoJoeyTransportConnectionState.Idle;
    public bool IsConnected { get; private set; }
    public string LastError { get; private set; } = "";
    public IReadOnlyList<string> SentMessages => sentMessages;
    public IReadOnlyList<string> SentRequestTypes => sentRequestTypes;
    public int GameplayMutationRequestCount { get; private set; }

    public void Connect(string webSocketUrl)
    {
        _ = webSocketUrl;
        IsConnected = true;
        State = MonoJoeyTransportConnectionState.ConnectedUnbound;
        Debug.Log("[MonoJoeyMockTransport] Chunk 7 mock transport connected. Mock/read-only; no backend mutation.", this);
        Connected?.Invoke();
    }

    public void Disconnect()
    {
        IsConnected = false;
        State = MonoJoeyTransportConnectionState.Disconnected;
        Disconnected?.Invoke();
    }

    public void SendJson(string json)
    {
        sentMessages.Add(json ?? "");
        string requestType = RequestType(json);
        sentRequestTypes.Add(requestType);
        if (GameplayMutationTypes.Contains(requestType))
        {
            GameplayMutationRequestCount++;
            Debug.LogError($"[MonoJoeyMockTransport] Gameplay mutation request was sent in read-only validation: {requestType}.", this);
        }

        if (requestType == "reconnect_session")
        {
            EmitServerMessage(CannedReconnectResultJson());
            return;
        }

        if (requestType == "get_snapshot")
        {
            EmitServerMessage(CannedSnapshotResultJson());
            return;
        }

        EmitError("unsupported_mock_request", $"Mock transport ignored unsupported request type {requestType}.");
    }

    public void EmitCannedReconnectResult()
    {
        EmitServerMessage(CannedReconnectResultJson());
    }

    public void EmitCannedSnapshotResult()
    {
        EmitServerMessage(CannedSnapshotResultJson());
    }

    public void EmitCannedError()
    {
        EmitServerMessage(@"{""type"":""error"",""payload"":{""code"":""mock_backend_error"",""message"":""Deterministic mock backend error.""}}");
    }

    public void EmitIgnoredBroadcast(long sequence)
    {
        EmitServerMessage($@"{{""type"":""dice_rolled"",""sequence"":{sequence},""sessionId"":""session_chunk_7"",""matchId"":""session_chunk_7"",""createdAtUtc"":""2026-05-11T00:02:00Z"",""payload"":{{""playerId"":""player-agentic"",""roll"":7}}}}");
    }

    public void EmitUnknownMessage()
    {
        EmitServerMessage(@"{""type"":""unknown_transport_probe"",""payload"":{""ignored"":true}}");
    }

    private void EmitError(string code, string message)
    {
        LastError = message ?? "";
        ErrorReceived?.Invoke(LastError);
        EmitServerMessage($@"{{""type"":""error"",""payload"":{{""code"":""{Escape(code)}"",""message"":""{Escape(message)}""}}}}");
    }

    private void EmitServerMessage(string json)
    {
        MessageReceived?.Invoke(json);
    }

    private static string RequestType(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "";
        }

        try
        {
            MonoJoeyServerEnvelope envelope = JsonUtility.FromJson<MonoJoeyServerEnvelope>(json);
            return envelope == null ? "" : envelope.type ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string Escape(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string CannedReconnectResultJson()
    {
        return @"{
  ""type"": ""reconnect_result"",
  ""payload"": {
    ""sessionId"": ""session_chunk_7"",
    ""playerId"": ""player-agentic"",
    ""lastEventSequence"": 7,
    ""snapshot"": {
      ""snapshotVersion"": 1,
      ""sessionId"": ""session_chunk_7"",
      ""status"": ""in_game"",
      ""gameStatus"": ""in_progress"",
      ""serverNowUtc"": ""2026-05-11T00:02:00Z"",
      ""matchId"": ""session_chunk_7"",
      ""phase"": ""awaiting_roll"",
      ""turn"": { ""currentPlayerId"": ""player-agentic"", ""turnIndex"": 12, ""hasRolledThisTurn"": false, ""hasResolvedTileThisTurn"": false, ""hasExecutedTileThisTurn"": false },
      ""players"": [
        { ""playerId"": ""player-agentic"", ""username"": ""Agentic Player"", ""tokenId"": ""token_agentic"", ""colorId"": ""gold"", ""money"": 1260, ""currentTileId"": ""start"", ""ownedPropertyIds"": [], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": { ""totalBorrowed"": 240, ""currentInterestRatePercent"": 25, ""nextTurnInterestDue"": 60, ""loanTier"": 2 }, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false },
        { ""playerId"": ""player-2"", ""username"": ""Blue Player"", ""tokenId"": ""token_blue"", ""colorId"": ""blue"", ""money"": 1580, ""currentTileId"": ""property_01"", ""ownedPropertyIds"": [], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": { ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 }, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }
      ],
      ""board"": {
        ""boardId"": ""chunk_7_board"",
        ""version"": 1,
        ""displayName"": ""Chunk 7 Board"",
        ""tiles"": [
          { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null },
          { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" },
          { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" },
          { ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": null }
        ]
      },
      ""activeAuction"": null
    }
  }
}";
    }

    private static string CannedSnapshotResultJson()
    {
        return @"{
  ""type"": ""snapshot_result"",
  ""payload"": {
    ""snapshotVersion"": 1,
    ""sessionId"": ""session_chunk_7"",
    ""status"": ""in_game"",
    ""gameStatus"": ""in_progress"",
    ""serverNowUtc"": ""2026-05-11T00:02:10Z"",
    ""matchId"": ""session_chunk_7"",
    ""phase"": ""auction_bidding"",
    ""turn"": { ""currentPlayerId"": ""player-agentic"", ""turnIndex"": 12, ""hasRolledThisTurn"": true, ""hasResolvedTileThisTurn"": true, ""hasExecutedTileThisTurn"": false },
    ""players"": [
      { ""playerId"": ""player-agentic"", ""username"": ""Agentic Player"", ""tokenId"": ""token_agentic"", ""colorId"": ""gold"", ""money"": 1190, ""currentTileId"": ""auction_test"", ""ownedPropertyIds"": [""property_02""], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": { ""totalBorrowed"": 240, ""currentInterestRatePercent"": 25, ""nextTurnInterestDue"": 60, ""loanTier"": 2 }, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false },
      { ""playerId"": ""player-2"", ""username"": ""Blue Player"", ""tokenId"": ""token_blue"", ""colorId"": ""blue"", ""money"": 1580, ""currentTileId"": ""property_01"", ""ownedPropertyIds"": [""property_01""], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": { ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 }, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }
    ],
    ""board"": {
      ""boardId"": ""chunk_7_board"",
      ""version"": 2,
      ""displayName"": ""Chunk 7 Board"",
      ""tiles"": [
        { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null },
        { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" },
        { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" },
        { ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": null }
      ]
    },
    ""activeAuction"": {
      ""propertyTileId"": ""auction_test"",
      ""triggeringPlayerId"": ""player-agentic"",
      ""status"": ""active"",
      ""startingBid"": 100,
      ""minimumBidIncrement"": 10,
      ""initialPreBidSeconds"": 5,
      ""bidResetSeconds"": 10,
      ""highestBid"": 330,
      ""highestBidderId"": ""player-2"",
      ""countdownDurationSeconds"": 9,
      ""timerEndsAtUtc"": ""2026-05-11T00:02:19Z"",
      ""bids"": [
        { ""bidderPlayerId"": ""player-agentic"", ""amount"": 310, ""placedAtUtc"": ""2026-05-11T00:02:11Z"" },
        { ""bidderPlayerId"": ""player-2"", ""amount"": 330, ""placedAtUtc"": ""2026-05-11T00:02:12Z"" }
      ]
    }
  }
}";
    }
}
