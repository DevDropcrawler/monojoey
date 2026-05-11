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

    public event Action CommandStateChanged;

    public bool IsCommandInFlight { get; private set; }
    public string InFlightRequestType { get; private set; } = "";
    public string InFlightLocalRequestId { get; private set; } = "";
    public string LastCommandRequestType { get; private set; } = "";
    public string LastCommandLocalRequestId { get; private set; } = "";
    public string LastCommandResult { get; private set; } = "";
    public string LastCommandResultType { get; private set; } = "";
    public string LastCommandResultSummary { get; private set; } = "";
    public DateTime LastCommandResultUtc { get; private set; } = DateTime.MinValue;
    public string LastCommandError { get; private set; } = "";
    public string LastBackendErrorCode { get; private set; } = "";
    public string LastBackendErrorMessage { get; private set; } = "";
    public bool IsWaitingForAuthoritativeSnapshot { get; private set; }
    public int CommandRequestCount { get; private set; }
    public DateTime LastCommandSentUtc { get; private set; } = DateTime.MinValue;
    public bool IsLiveBackend => sessionClient != null && sessionClient.Mode == MonoJoeySessionClientMode.LiveBackend;
    public bool IsTransportConnected => sessionClient != null && sessionClient.IsTransportConnected;
    public bool IsGameplayCommandModeAvailable => sessionClient != null && (sessionClient.Mode == MonoJoeySessionClientMode.LiveBackend || sessionClient.EnableMockGameplayCommandTestMode);
    public bool IsMockGameplayCommandTestMode => sessionClient != null && sessionClient.EnableMockGameplayCommandTestMode;
    public bool IsBoundToIdentity => sessionClient != null && sessionClient.IsBoundToIdentity;
    public string LastBlockedCommandType { get; private set; } = "";
    public string LastBlockedReason { get; private set; } = "";
    public string LastInFlightStateLog { get; private set; } = "";

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

    public bool CanRollDice(out string reason)
    {
        return CanSendCommand(MonoJoeyTransportMessageTypes.RollDice, out reason);
    }

    public bool TryResolveTile()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.ResolveTile);
    }

    public bool CanResolveTile(out string reason)
    {
        return CanSendCommand(MonoJoeyTransportMessageTypes.ResolveTile, out reason);
    }

    public bool TryExecuteTile()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.ExecuteTile);
    }

    public bool CanExecuteTile(out string reason)
    {
        return CanSendCommand(MonoJoeyTransportMessageTypes.ExecuteTile, out reason);
    }

    public bool TryEndTurn()
    {
        return TrySendSessionPlayerCommand(MonoJoeyTransportMessageTypes.EndTurn);
    }

    public bool CanEndTurn(out string reason)
    {
        return CanSendCommand(MonoJoeyTransportMessageTypes.EndTurn, out reason);
    }

    public bool CanPlaceBid(int amount, out string reason)
    {
        if (amount <= 0)
        {
            reason = $"amount must be positive, got {amount}";
            RecordBlockedCommand(MonoJoeyTransportMessageTypes.PlaceBid, reason);
            return false;
        }

        return CanSendCommand(MonoJoeyTransportMessageTypes.PlaceBid, out reason);
    }

    public bool TryPlaceBid(int amount)
    {
        if (!CanPlaceBid(amount, out string reason))
        {
            LastCommandError = $"place_bid blocked: {reason}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            NotifyCommandStateChanged();
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
            RecordBlockedCommand(type, reason);
            return false;
        }

        if (sessionClient == null)
        {
            reason = "session client is not configured";
            RecordBlockedCommand(type, reason);
            return false;
        }

        if (IsCommandInFlight)
        {
            reason = $"command {Display(InFlightRequestType)} is already in flight";
            RecordBlockedCommand(type, reason);
            return false;
        }

        if (!sessionClient.CanSendGameplayCommand(type, out reason))
        {
            RecordBlockedCommand(type, reason);
            return false;
        }

        reason = "";
        return true;
    }

    public void HandleDirectCommandResult(string resultType, string rawJson)
    {
        string safeResultType = resultType ?? "";
        LastCommandResultType = safeResultType;
        LastCommandResult = string.IsNullOrWhiteSpace(rawJson) ? safeResultType : rawJson;
        LastCommandResultSummary = $"direct result {Display(safeResultType)} accepted";
        LastCommandResultUtc = DateTime.UtcNow;
        LastCommandError = "";
        LastBackendErrorCode = "";
        LastBackendErrorMessage = "";

        ResultToRequestType.TryGetValue(safeResultType, out string matchingRequestType);
        bool clearsInFlight = IsCommandInFlight
            && !string.IsNullOrWhiteSpace(matchingRequestType)
            && string.Equals(matchingRequestType, InFlightRequestType, StringComparison.Ordinal);

        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] Direct command result received: result={Display(safeResultType)}, matchingRequest={Display(matchingRequestType)}, clearsInFlight={clearsInFlight}. No local gameplay UI mutation.", this);

        if (clearsInFlight)
        {
            ClearInFlight($"direct result {Display(safeResultType)}");
            SetWaitingForAuthoritativeSnapshot(true, $"direct result {Display(safeResultType)}");
            return;
        }

        NotifyCommandStateChanged();
    }

    public void HandleBackendError(string code, string message)
    {
        LastBackendErrorCode = code ?? "";
        LastBackendErrorMessage = message ?? "";
        LastCommandError = $"backend error code={Display(code)} message={Display(message)}";
        Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}. Clearing any in-flight command without local compensation.", this);
        ClearInFlight("backend error");
        SetWaitingForAuthoritativeSnapshot(false, "backend error");
    }

    public void HandleAuthoritativeHydration(string messageType)
    {
        if (IsWaitingForAuthoritativeSnapshot)
        {
            SetWaitingForAuthoritativeSnapshot(false, $"authoritative hydration {Display(messageType)}");
        }

        if (IsCommandInFlight)
        {
            Debug.Log($"[MonoJoeyGameplayCommandDispatcher] {Display(messageType)} observed; clearing in-flight command {Display(InFlightRequestType)}.", this);
            ClearInFlight($"authoritative hydration {Display(messageType)}");
            return;
        }

        NotifyCommandStateChanged();
    }

    public void HandleDisconnectedOrTransportError(string reason)
    {
        if (!IsCommandInFlight && !IsWaitingForAuthoritativeSnapshot)
        {
            return;
        }

        LastCommandError = $"{Display(reason)} cleared in-flight command {Display(InFlightRequestType)}";
        Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}. No retry attempted.", this);
        ClearInFlight(reason);
        SetWaitingForAuthoritativeSnapshot(false, reason);
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
        ClearInFlight("timeout");
        SetWaitingForAuthoritativeSnapshot(false, "timeout");
    }

    private bool TrySendSessionPlayerCommand(string requestType)
    {
        if (!CanSendCommand(requestType, out string reason))
        {
            LastCommandError = $"{requestType} blocked: {reason}.";
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            NotifyCommandStateChanged();
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
        LastInFlightStateLog = $"in-flight started type={requestType}, localRequestId={localRequestId}";
        LastCommandRequestType = requestType;
        LastCommandLocalRequestId = localRequestId;
        inFlightStartedRealtime = Time.realtimeSinceStartup;
        LastCommandSentUtc = DateTime.UtcNow;
        LastCommandError = "";
        LastBackendErrorCode = "";
        LastBackendErrorMessage = "";
        LastBlockedCommandType = "";
        LastBlockedReason = "";
        SetWaitingForAuthoritativeSnapshot(false, "new command");

        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] Command attempt accepted for send: type={requestType}, localRequestId={localRequestId}, mode={(IsMockGameplayCommandTestMode ? "mock-command-test" : "live")}.", this);

        if (!sessionClient.TrySendGameplayCommandJson(requestType, json, localRequestId, out string reason))
        {
            ClearInFlight("session gateway rejected send");
            LastCommandError = $"{requestType} blocked by session gateway: {reason}.";
            RecordBlockedCommand(requestType, reason);
            Debug.LogWarning($"[MonoJoeyGameplayCommandDispatcher] {LastCommandError}", this);
            NotifyCommandStateChanged();
            return false;
        }

        CommandRequestCount++;
        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] Sent gameplay command intent type={requestType}, localRequestId={localRequestId}. No local gameplay UI mutation.", this);
        NotifyCommandStateChanged();
        return true;
    }

    private void ClearInFlight(string reason)
    {
        string previousType = InFlightRequestType;
        string previousRequestId = InFlightLocalRequestId;
        IsCommandInFlight = false;
        InFlightRequestType = "";
        InFlightLocalRequestId = "";
        inFlightStartedRealtime = 0f;
        LastInFlightStateLog = $"in-flight cleared type={Display(previousType)}, localRequestId={Display(previousRequestId)}, reason={Display(reason)}";
        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] {LastInFlightStateLog}.", this);
        NotifyCommandStateChanged();
    }

    private void RecordBlockedCommand(string type, string reason)
    {
        LastBlockedCommandType = type ?? "";
        LastBlockedReason = reason ?? "";
    }

    private void SetWaitingForAuthoritativeSnapshot(bool waiting, string reason)
    {
        if (IsWaitingForAuthoritativeSnapshot == waiting)
        {
            NotifyCommandStateChanged();
            return;
        }

        IsWaitingForAuthoritativeSnapshot = waiting;
        Debug.Log($"[MonoJoeyGameplayCommandDispatcher] waitingForAuthoritativeSnapshot={waiting}, reason={Display(reason)}.", this);
        NotifyCommandStateChanged();
    }

    private void NotifyCommandStateChanged()
    {
        CommandStateChanged?.Invoke();
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
