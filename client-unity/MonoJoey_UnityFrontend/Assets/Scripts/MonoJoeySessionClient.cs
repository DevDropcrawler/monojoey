using System;
using System.Collections;
using UnityEngine;

public sealed class MonoJoeySessionClient : MonoBehaviour
{
    [SerializeField] private MonoJoeySessionClientMode mode = MonoJoeySessionClientMode.MockValidation;
    [SerializeField] private string webSocketUrl = "ws://127.0.0.1:5000/ws";
    [SerializeField] private string sessionId = "";
    [SerializeField] private string playerId = "";
    [SerializeField] private bool autoConnect;
    [SerializeField] private bool autoReconnect = true;
    [SerializeField] private float reconnectInitialBackoffSeconds = 1f;
    [SerializeField] private float reconnectMaxBackoffSeconds = 8f;
    [SerializeField] private bool requestSnapshotAfterBroadcast = true;
    [SerializeField] private bool allowMockFallbackOnLiveFailure;
    [SerializeField] private bool enableMockGameplayCommandTestMode;
    [SerializeField] private float broadcastSnapshotDebounceSeconds = 0.5f;
    [SerializeField] private SnapshotHydrator snapshotHydrator;
    [SerializeField] private MonoJoeyBackendMessageRouter messageRouter;
    [SerializeField] private MonoJoeyGameplayCommandDispatcher gameplayCommandDispatcher;

    private IMonoJoeyTransport transport;
    private MonoJoeySessionClientMode transportMode;
    private bool hasTransportMode;
    private Coroutine reconnectCoroutine;
    private Coroutine broadcastSnapshotCoroutine;
    private bool disconnectRequested;
    private bool connectedOnce;
    private bool boundToIdentity;
    private bool usingMockFallback;

    public event Action StatusChanged;

    public MonoJoeySessionClientMode Mode => mode;
    public MonoJoeyTransportConnectionState State { get; private set; } = MonoJoeyTransportConnectionState.Idle;
    public string WebSocketUrl => webSocketUrl;
    public string SessionId => sessionId;
    public string PlayerId => playerId;
    public bool IsUsingMockFallback => usingMockFallback;
    public bool IsBoundToIdentity => boundToIdentity;
    public bool IsTransportConnected => transport != null && transport.IsConnected;
    public bool RequestSnapshotAfterBroadcastEnabled => requestSnapshotAfterBroadcast;
    public bool AllowMockFallbackOnLiveFailure => allowMockFallbackOnLiveFailure;
    public bool EnableMockGameplayCommandTestMode => enableMockGameplayCommandTestMode;
    public string LastError { get; private set; } = "";
    public string LastSentRequestType { get; private set; } = "";
    public int ReadOnlyRequestCount { get; private set; }
    public int ReconnectAttemptCount { get; private set; }
    public int GameplayCommandRequestCount { get; private set; }
    public DateTime LastRequestSentUtc { get; private set; } = DateTime.MinValue;

    public void Configure(
        MonoJoeySessionClientMode configuredMode,
        string configuredWebSocketUrl,
        string configuredSessionId,
        string configuredPlayerId,
        SnapshotHydrator hydrator,
        MonoJoeyBackendMessageRouter router,
        bool configuredRequestSnapshotAfterBroadcast,
        bool configuredAllowMockFallbackOnLiveFailure)
    {
        Configure(
            configuredMode,
            configuredWebSocketUrl,
            configuredSessionId,
            configuredPlayerId,
            hydrator,
            router,
            configuredRequestSnapshotAfterBroadcast,
            configuredAllowMockFallbackOnLiveFailure,
            enableMockGameplayCommandTestMode);
    }

    public void Configure(
        MonoJoeySessionClientMode configuredMode,
        string configuredWebSocketUrl,
        string configuredSessionId,
        string configuredPlayerId,
        SnapshotHydrator hydrator,
        MonoJoeyBackendMessageRouter router,
        bool configuredRequestSnapshotAfterBroadcast,
        bool configuredAllowMockFallbackOnLiveFailure,
        bool configuredEnableMockGameplayCommandTestMode)
    {
        mode = configuredMode;
        webSocketUrl = configuredWebSocketUrl ?? "";
        sessionId = configuredSessionId ?? "";
        playerId = configuredPlayerId ?? "";
        snapshotHydrator = hydrator;
        messageRouter = router;
        requestSnapshotAfterBroadcast = configuredRequestSnapshotAfterBroadcast;
        allowMockFallbackOnLiveFailure = configuredAllowMockFallbackOnLiveFailure;
        enableMockGameplayCommandTestMode = configuredEnableMockGameplayCommandTestMode;
        EnsureTransportAndRouter();
        ConfigureMockTransportCommandTestMode();
        StatusChanged?.Invoke();
    }

    public void BindTransportForValidation(IMonoJoeyTransport configuredTransport)
    {
        UnsubscribeTransport();
        transport = configuredTransport;
        transportMode = mode;
        hasTransportMode = true;
        SubscribeTransport();
        ConfigureMockTransportCommandTestMode();
        EnsureRouter();
    }

    public void Connect()
    {
        disconnectRequested = false;
        boundToIdentity = false;
        EnsureTransportAndRouter();
        SetState(MonoJoeyTransportConnectionState.Connecting);
        transport.Connect(webSocketUrl);
    }

    public void Disconnect()
    {
        disconnectRequested = true;
        boundToIdentity = false;
        StopReconnect();
        StopBroadcastSnapshotDebounce();
        gameplayCommandDispatcher?.HandleDisconnectedOrTransportError("disconnect");
        transport?.Disconnect();
        SetState(MonoJoeyTransportConnectionState.Disconnected);
    }

    public void RequestSnapshot()
    {
        if (!CanSendSnapshotRequest())
        {
            return;
        }

        SendReadOnlyRequest("get_snapshot");
    }

    public void ReconnectSession()
    {
        if (!CanSendConnectedIdentityRequest("reconnect_session"))
        {
            return;
        }

        boundToIdentity = false;
        ReconnectAttemptCount++;
        SetState(connectedOnce ? MonoJoeyTransportConnectionState.Reconnecting : MonoJoeyTransportConnectionState.Hydrating);
        SendReadOnlyRequest("reconnect_session");
    }

    public void RequestSnapshotAfterBroadcast()
    {
        if (!requestSnapshotAfterBroadcast || State != MonoJoeyTransportConnectionState.BoundLive)
        {
            return;
        }

        if (broadcastSnapshotCoroutine != null)
        {
            return;
        }

        broadcastSnapshotCoroutine = StartCoroutine(DebouncedBroadcastSnapshotRequest());
    }

    public void MarkBoundAfterHydration()
    {
        connectedOnce = true;
        boundToIdentity = true;
        gameplayCommandDispatcher?.HandleAuthoritativeHydration(MonoJoeyTransportMessageTypes.ReconnectResult);
        SetState(MonoJoeyTransportConnectionState.BoundLive);
    }

    public void ReportAuthoritativeHydration(string messageType)
    {
        gameplayCommandDispatcher?.HandleAuthoritativeHydration(messageType);
        StatusChanged?.Invoke();
    }

    public void ReportReconnectHydrationFailure(string message)
    {
        boundToIdentity = false;
        LastError = message ?? "";
        SetState(transport != null && transport.IsConnected
            ? MonoJoeyTransportConnectionState.ConnectedUnbound
            : MonoJoeyTransportConnectionState.Error);
    }

    public void ReportBackendError(string message)
    {
        LastError = message ?? "";
        gameplayCommandDispatcher?.HandleBackendError("", LastError);
        StatusChanged?.Invoke();
    }

    public void ReportBackendError(string code, string message)
    {
        LastError = message ?? "";
        gameplayCommandDispatcher?.HandleBackendError(code, LastError);
        StatusChanged?.Invoke();
    }

    public void ReportDirectCommandResult(string resultType, string rawJson)
    {
        gameplayCommandDispatcher?.HandleDirectCommandResult(resultType, rawJson);
        StatusChanged?.Invoke();
    }

    public void BindGameplayCommandDispatcher(MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        if (dispatcher == null && gameplayCommandDispatcher != null)
        {
            gameplayCommandDispatcher = null;
            return;
        }

        if (dispatcher != null)
        {
            gameplayCommandDispatcher = dispatcher;
        }
    }

    public bool CanSendGameplayCommand(string requestType, out string reason)
    {
        if (!IsApprovedGameplayCommand(requestType))
        {
            reason = $"command type {Display(requestType)} is not approved";
            return false;
        }

        if (mode == MonoJoeySessionClientMode.MockValidation && !enableMockGameplayCommandTestMode)
        {
            reason = "mock validation blocks gameplay commands unless dispatcher test mode is enabled";
            return false;
        }

        if (mode != MonoJoeySessionClientMode.LiveBackend && !enableMockGameplayCommandTestMode)
        {
            reason = $"mode {mode} does not allow gameplay commands";
            return false;
        }

        if (transport == null || !transport.IsConnected)
        {
            reason = "transport is disconnected";
            return false;
        }

        if (!HasSessionAndPlayer())
        {
            reason = "sessionId/playerId is missing";
            return false;
        }

        if (!boundToIdentity || State != MonoJoeyTransportConnectionState.BoundLive)
        {
            reason = "socket is not bound to a hydrated session/player identity";
            return false;
        }

        reason = "";
        return true;
    }

    public bool TrySendGameplayCommandJson(string requestType, string json, string localRequestId, out string reason)
    {
        if (!CanSendGameplayCommand(requestType, out reason))
        {
            Debug.LogWarning($"[MonoJoeySessionClient] Gameplay command {Display(requestType)} skipped: {reason}.", this);
            return false;
        }

        LastSentRequestType = requestType;
        LastRequestSentUtc = DateTime.UtcNow;
        GameplayCommandRequestCount++;
        Debug.Log($"[MonoJoeySessionClient] Sending gameplay command intent type={requestType}, localRequestId={Display(localRequestId)}. Backend remains authoritative.", this);
        transport.SendJson(json);
        StatusChanged?.Invoke();
        return true;
    }

    private void Awake()
    {
        EnsureTransportAndRouter();
    }

    private void Start()
    {
        if (autoConnect)
        {
            Connect();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeTransport();
        transport?.Disconnect();
    }

    private void EnsureTransportAndRouter()
    {
        if (transport == null || !hasTransportMode || transportMode != mode)
        {
            bool replacingTransport = transport != null && hasTransportMode && transportMode != mode;
            if (replacingTransport)
            {
                transport.Disconnect();
                boundToIdentity = false;
            }

            UnsubscribeTransport();
            if (mode == MonoJoeySessionClientMode.MockValidation)
            {
                MonoJoeyMockTransport mockTransport = GetComponent<MonoJoeyMockTransport>();
                if (mockTransport == null)
                {
                    mockTransport = gameObject.AddComponent<MonoJoeyMockTransport>();
                }

                transport = mockTransport;
            }
            else
            {
                MonoJoeyWebSocketTransport webSocketTransport = GetComponent<MonoJoeyWebSocketTransport>();
                if (webSocketTransport == null)
                {
                    webSocketTransport = gameObject.AddComponent<MonoJoeyWebSocketTransport>();
                }

                transport = webSocketTransport;
            }

            transportMode = mode;
            hasTransportMode = true;
            SubscribeTransport();
            ConfigureMockTransportCommandTestMode();
        }

        EnsureRouter();
    }

    private void EnsureRouter()
    {
        if (messageRouter == null)
        {
            messageRouter = GetComponent<MonoJoeyBackendMessageRouter>();
            if (messageRouter == null)
            {
                messageRouter = gameObject.AddComponent<MonoJoeyBackendMessageRouter>();
            }
        }

        messageRouter.Configure(snapshotHydrator, this, requestSnapshotAfterBroadcast);
    }

    private void SubscribeTransport()
    {
        if (transport == null)
        {
            return;
        }

        transport.Connected += HandleTransportConnected;
        transport.Disconnected += HandleTransportDisconnected;
        transport.MessageReceived += HandleTransportMessage;
        transport.ErrorReceived += HandleTransportError;
    }

    private void UnsubscribeTransport()
    {
        if (transport == null)
        {
            return;
        }

        transport.Connected -= HandleTransportConnected;
        transport.Disconnected -= HandleTransportDisconnected;
        transport.MessageReceived -= HandleTransportMessage;
        transport.ErrorReceived -= HandleTransportError;
    }

    private void HandleTransportConnected()
    {
        boundToIdentity = false;
        SetState(MonoJoeyTransportConnectionState.ConnectedUnbound);
        Debug.Log($"[MonoJoeySessionClient] Connected in mode={mode}, session={Display(sessionId)}, player={Display(playerId)}. Read-only transport; no backend mutation.", this);
        if (HasSessionAndPlayer())
        {
            ReconnectSession();
            return;
        }

        Debug.Log("[MonoJoeySessionClient] Connected without sessionId/playerId; no read-only request sent.", this);
    }

    private void HandleTransportDisconnected()
    {
        boundToIdentity = false;
        gameplayCommandDispatcher?.HandleDisconnectedOrTransportError("transport disconnected");
        if (disconnectRequested)
        {
            SetState(MonoJoeyTransportConnectionState.Disconnected);
            return;
        }

        SetState(MonoJoeyTransportConnectionState.Disconnected);
        if (mode == MonoJoeySessionClientMode.LiveBackend && autoReconnect)
        {
            StartReconnect();
        }
    }

    private void HandleTransportMessage(string json)
    {
        messageRouter?.RouteRawMessage(json);
        StatusChanged?.Invoke();
    }

    private void HandleTransportError(string message)
    {
        LastError = message ?? "";
        gameplayCommandDispatcher?.HandleDisconnectedOrTransportError($"transport error: {LastError}");
        SetState(MonoJoeyTransportConnectionState.Error);
        Debug.LogWarning($"[MonoJoeySessionClient] Transport error: {Display(LastError)}. Read-only transport; no backend mutation.", this);
        if (mode == MonoJoeySessionClientMode.LiveBackend && allowMockFallbackOnLiveFailure && !usingMockFallback)
        {
            ActivateMockFallback();
        }
    }

    private void StartReconnect()
    {
        if (reconnectCoroutine == null)
        {
            reconnectCoroutine = StartCoroutine(AutoReconnectLoop());
        }
    }

    private void StopReconnect()
    {
        if (reconnectCoroutine != null)
        {
            StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = null;
        }
    }

    private IEnumerator AutoReconnectLoop()
    {
        float backoff = Mathf.Max(0.1f, reconnectInitialBackoffSeconds);
        while (!disconnectRequested && mode == MonoJoeySessionClientMode.LiveBackend)
        {
            SetState(MonoJoeyTransportConnectionState.Reconnecting);
            yield return new WaitForSeconds(backoff);
            if (disconnectRequested)
            {
                yield break;
            }

            transport?.Connect(webSocketUrl);
            backoff = Mathf.Min(Mathf.Max(backoff * 2f, 0.1f), Mathf.Max(reconnectMaxBackoffSeconds, backoff));
            reconnectCoroutine = null;
            yield break;
        }

        reconnectCoroutine = null;
    }

    private IEnumerator DebouncedBroadcastSnapshotRequest()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, broadcastSnapshotDebounceSeconds));
        broadcastSnapshotCoroutine = null;
        RequestSnapshot();
    }

    private void StopBroadcastSnapshotDebounce()
    {
        if (broadcastSnapshotCoroutine != null)
        {
            StopCoroutine(broadcastSnapshotCoroutine);
            broadcastSnapshotCoroutine = null;
        }
    }

    private bool CanSendConnectedIdentityRequest(string requestType)
    {
        if (transport == null || !transport.IsConnected)
        {
            Debug.LogWarning($"[MonoJoeySessionClient] Read-only {requestType} skipped because transport is disconnected.", this);
            return false;
        }

        if (!HasSessionAndPlayer())
        {
            Debug.LogWarning($"[MonoJoeySessionClient] Read-only {requestType} skipped because sessionId/playerId is missing.", this);
            return false;
        }

        return true;
    }

    private bool CanSendSnapshotRequest()
    {
        if (!CanSendConnectedIdentityRequest("get_snapshot"))
        {
            return false;
        }

        if (!boundToIdentity || State != MonoJoeyTransportConnectionState.BoundLive)
        {
            Debug.LogWarning("[MonoJoeySessionClient] Read-only get_snapshot skipped because the socket is not bound to a hydrated session/player identity.", this);
            return false;
        }

        return true;
    }

    private void SendReadOnlyRequest(string requestType)
    {
        MonoJoeyReadOnlyRequestEnvelope envelope = new MonoJoeyReadOnlyRequestEnvelope
        {
            type = requestType,
            payload = new MonoJoeySessionPlayerPayload
            {
                sessionId = sessionId,
                playerId = playerId
            }
        };

        LastSentRequestType = requestType;
        ReadOnlyRequestCount++;
        LastRequestSentUtc = DateTime.UtcNow;
        transport.SendJson(JsonUtility.ToJson(envelope));
        StatusChanged?.Invoke();
    }

    private void ActivateMockFallback()
    {
        usingMockFallback = true;
        Debug.LogWarning("[MonoJoeySessionClient] Live backend unavailable; explicit mock fallback activated. UI is no longer live backend state.", this);
        UnsubscribeTransport();
        MonoJoeyMockTransport mockTransport = gameObject.AddComponent<MonoJoeyMockTransport>();
        transport = mockTransport;
        SubscribeTransport();
        mode = MonoJoeySessionClientMode.MockValidation;
        Connect();
    }

    private void ConfigureMockTransportCommandTestMode()
    {
        MonoJoeyMockTransport mockTransport = transport as MonoJoeyMockTransport;
        if (mockTransport != null)
        {
            mockTransport.SetGameplayCommandTestResponsesEnabled(enableMockGameplayCommandTestMode);
        }
    }

    private static bool IsApprovedGameplayCommand(string requestType)
    {
        return string.Equals(requestType, MonoJoeyTransportMessageTypes.RollDice, StringComparison.Ordinal)
            || string.Equals(requestType, MonoJoeyTransportMessageTypes.ResolveTile, StringComparison.Ordinal)
            || string.Equals(requestType, MonoJoeyTransportMessageTypes.ExecuteTile, StringComparison.Ordinal)
            || string.Equals(requestType, MonoJoeyTransportMessageTypes.EndTurn, StringComparison.Ordinal)
            || string.Equals(requestType, MonoJoeyTransportMessageTypes.PlaceBid, StringComparison.Ordinal);
    }

    private bool HasSessionAndPlayer()
    {
        return !string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(playerId);
    }

    private void SetState(MonoJoeyTransportConnectionState nextState)
    {
        State = nextState;
        StatusChanged?.Invoke();
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
