using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MonoJoeyGameplayCommandDispatcher : MonoBehaviour
{
    private static readonly HashSet<string> ApprovedCommandTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        MonoJoeyTransportMessageTypes.RollDice,
        MonoJoeyTransportMessageTypes.ResolveTile,
        MonoJoeyTransportMessageTypes.ExecuteTile,
        MonoJoeyTransportMessageTypes.EndTurn,
        MonoJoeyTransportMessageTypes.PlaceBid
    };

    private static readonly Dictionary<string, string> ResultToRequestType = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { MonoJoeyTransportMessageTypes.RollResult, MonoJoeyTransportMessageTypes.RollDice },
        { MonoJoeyTransportMessageTypes.ResolveTileResult, MonoJoeyTransportMessageTypes.ResolveTile },
        { MonoJoeyTransportMessageTypes.ExecuteTileResult, MonoJoeyTransportMessageTypes.ExecuteTile },
        { MonoJoeyTransportMessageTypes.EndTurnResult, MonoJoeyTransportMessageTypes.EndTurn },
        { MonoJoeyTransportMessageTypes.BidResult, MonoJoeyTransportMessageTypes.PlaceBid }
    };

    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private float commandTimeoutSeconds = 15f;

    private float inFlightStartedRealtime;

    public bool IsCommandInFlight { get; private set; }
    public string InFlightRequestType { get; private set; } = "";
    public string InFlightLocalRequestId { get; private set; } = "";
    public string LastCommandRequestType { get; private set; } = "";
    public string LastCommandLocalRequestId { get; private set; } = "";
    public string LastCommandResult { get; private set; } = "";
    public string LastCommandError { get; private set; } = "";
    public int CommandRequestCount { get; private set; }
    public DateTime LastCommandSentUtc { get; private set; } = DateTime.MinValue;
    public bool IsLiveBackend => sessionClient != null && sessionClient.Mode == MonoJoeySessionClientMode.LiveBackend;
    public bool IsBoundToIdentity => sessionClient != null && sessionClient.IsBoundToIdentity;

    public void Configure(MonoJoeySessionClient client)
    {
        if (sessionClient != null && sessionClient != client)
        {
            sessionClient.BindGameplayCommandDispatcher(null);
        }

        sessionClient = client;
        sessionClient?.BindGameplayCommandDispatcher(this);
    }

    public bool TryRollDice()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.RollDice);
    }

    public bool TryResolveTile()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.ResolveTile);
    }

    public bool TryExecuteTile()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.ExecuteTile);
    }

    public bool TryEndTurn()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.EndTurn);
    }

    public bool TryPlaceBid(int amount)
    {
        if (amount <= 0)
        {
            LastCommandError = $"place_bid blocked: amount must be positive, got {amount}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            return false;
        }

        if (!CanSendCommand(MonoJoeyTransportMessageTypes.PlaceBid, out string reason))
        {
            LastCommandError = $"place_bid blocked: {reason}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            return false;
        }

        MonoJoeyGameplayPlaceBidRequestEnvelope envelope = new MonoJoeyGameplayPlaceBidRequestEnvelope
        {
            type = MonoJoeyTransportMessageTypes.PlaceBid,
            payload = new MonoJoeyGameplayPlaceBidPayload
            {
                sessionId = sessionClient.SessionId,
                playerId = sessionClient.PlayerId,
                amount = amount
            }
        };

        return SendCommandJson(MonoJoeyTransportMessageTypes.PlaceBid, JsonUtility.ToJson(envelope));
    }

    public bool CanSendCommand(string type, out string reason)
    {
        if (!ApprovedCommandTypes.Contains(type ?? ""))
        {
            reason = $"command type {Display(type)} is not approved";
            return false;
        }

        if (IsCommandInFlight)
        {
            reason = $"command {Display(InFlightRequestType)} is already in flight";
            return false;
        }

        if (sessionClient == null)
        {
            reason = "session client is not configured";
            return false;
        }

        return sessionClient.CanSendGameplayCommand(type, out reason);
    }

    public void HandleDirectCommandResult(string resultType, string rawJson)
    {
        string safeResultType = resultType ?? "";
        LastCommandResult = string.IsNullOrWhiteSpace(rawJson) ? safeResultType : rawJson;
        LastCommandError = "";

        ResultToRequestType.TryGetValue(safeResultType, out string matchingRequestType);
        bool clearsInFlight = IsCommandInFlight
            && !string.IsNullOrWhiteSpace(matchingRequestType)
            && string.Equals(matchingRequestType, InFlightRequestType, StringComparison.Ordinal);

        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] Direct command result received: result={Display(safeResultType)}, matchingRequest={Display(matchingRequestType)}, clearsInFlight={clearsInFlight}. No local gameplay UI mutation.", this);

        if (clearsInFlight)
        {
            ClearInFlight();
        }
    }

    public void HandleBackendError(string code, string message)
    {
        LastCommandError = $"backend error code={Display(code)} message={Display(message)}";
        Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}. Clearing any in-flight command without local compensation.", this);
        ClearInFlight();
    }

    public void HandleAuthoritativeHydration(string messageType)
    {
        if (!IsCommandInFlight)
        {
            return;
        }

        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] {Display(messageType)} observed; clearing in-flight command {Display(InFlightRequestType)}.", this);
        ClearInFlight();
    }

    public void HandleDisconnectedOrTransportError(string reason)
    {
        if (!IsCommandInFlight)
        {
            return;
        }

        LastCommandError = $"{Display(reason)} cleared in-flight command {Display(InFlightRequestType)}";
        Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}. No retry attempted.", this);
        ClearInFlight();
    }

    private void Awake()
    {
        if (sessionClient == null)
        {
            sessionClient = GetComponent<MonoJoeySessionClient>();
        }

        sessionClient?.BindGameplayCommandDispatcher(this);
    }

    private void OnDestroy()
    {
        if (sessionClient != null)
        {
            sessionClient.BindGameplayCommandDispatcher(null);
        }
    }

    private void Update()
    {
        if (!IsCommandInFlight)
        {
            return;
        }

        float timeout = Mathf.Max(1f, commandTimeoutSeconds);
        if (Time.realtimeSinceStartup - inFlightStartedRealtime < timeout)
        {
            return;
        }

        LastCommandError = $"command {Display(InFlightRequestType)} timed out after {timeout:0.0}s";
        Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}. No retry attempted.", this);
        ClearInFlight();
    }

    private bool TrySendSessionPlayerCommand(string requestType)
    {
        if (!CanSendCommand(requestType, out string reason))
        {
            LastCommandError = $"{requestType} blocked: {reason}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            return false;
        }

        MonoJoeyGameplaySessionPlayerRequestEnvelope envelope = new MonoJoeyGameplaySessionPlayerRequestEnvelope
        {
            type = requestType,
            payload = new MonoJoeySessionPlayerPayload
            {
                sessionId = sessionClient.SessionId,
                playerId = sessionClient.PlayerId
            }
        };

        return SendCommandJson(requestType, JsonUtility.ToJson(envelope));
    }

    private bool SendCommandJson(string requestType, string json)
    {
        string localRequestId = Guid.NewGuid().ToString("N");
        IsCommandInFlight = true;
        InFlightRequestType = requestType;
        InFlightLocalRequestId = localRequestId;
        LastCommandRequestType = requestType;
        LastCommandLocalRequestId = localRequestId;
        inFlightStartedRealtime = Time.realtimeSinceStartup;
        LastCommandSentUtc = DateTime.UtcNow;
        LastCommandError = "";

        if (!sessionClient.TrySendGameplayCommandJson(requestType, json, localRequestId, out string reason))
        {
            ClearInFlight();
            LastCommandError = $"{requestType} blocked by session gateway: {reason}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            return false;
        }

        CommandRequestCount++;
        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] Sent gameplay command intent type={requestType}, localRequestId={localRequestId}. No local gameplay UI mutation.", this);
        return true;
    }

    private void ClearInFlight()
    {
        IsCommandInFlight = false;
        InFlightRequestType = "";
        InFlightLocalRequestId = "";
        inFlightStartedRealtime = 0f;
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
