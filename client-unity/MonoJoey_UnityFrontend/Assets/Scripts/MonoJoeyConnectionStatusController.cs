using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonoJoeyConnectionStatusController : MonoBehaviour
{
    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private MonoJoeyBackendMessageRouter messageRouter;
    [SerializeField] private MonoJoeyGameplayCommandDispatcher gameplayCommandDispatcher;
    [SerializeField] private SnapshotHydrator snapshotHydrator;
    [SerializeField] private bool createRuntimeControls = true;
    [SerializeField] private Text modeText;
    [SerializeField] private Text stateText;
    [SerializeField] private Text sessionText;
    [SerializeField] private Text lastMessageText;
    [SerializeField] private Text commandStatusText;
    [SerializeField] private Text lastSequenceText;
    [SerializeField] private Text lastSnapshotText;
    [SerializeField] private Text lastErrorText;
    [SerializeField] private InputField webSocketUrlInput;
    [SerializeField] private InputField sessionIdInput;
    [SerializeField] private InputField playerIdInput;
    [SerializeField] private Dropdown modeDropdown;
    [SerializeField] private Toggle mockFallbackToggle;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button snapshotButton;

    public string LastRenderedStatus { get; private set; } = "";

    public void Configure(MonoJoeySessionClient client, MonoJoeyBackendMessageRouter router)
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= Refresh;
        }

        sessionClient = client;
        messageRouter = router;
        gameplayCommandDispatcher = client == null ? gameplayCommandDispatcher : client.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        snapshotHydrator = router == null ? snapshotHydrator : router.Hydrator;
        if (sessionClient != null)
        {
            sessionClient.StatusChanged += Refresh;
        }

        SyncControlsFromClient();
        Refresh();
    }

    private void Awake()
    {
        EnsureRuntimeControls();
        WireControls();
    }

    private void OnEnable()
    {
        EnsureRuntimeControls();
        WireControls();
        if (sessionClient != null)
        {
            sessionClient.StatusChanged += Refresh;
        }

        SyncControlsFromClient();
        Refresh();
    }

    private void OnDisable()
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= Refresh;
        }
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        string mode = sessionClient == null ? "--" : sessionClient.Mode.ToString();
        string state = sessionClient == null ? "--" : sessionClient.State.ToString();
        string bound = sessionClient != null && sessionClient.IsBoundToIdentity ? "bound" : "unbound";
        string session = sessionClient == null ? "--" : $"{Display(sessionClient.SessionId)} / {Display(sessionClient.PlayerId)}";
        string url = sessionClient == null ? "--" : Display(sessionClient.WebSocketUrl);
        string lastRequest = sessionClient == null ? "--" : Display(sessionClient.LastSentRequestType);
        string lastType = messageRouter == null ? "--" : Display(messageRouter.LastMessageType);
        string lastMessageTime = messageRouter == null || messageRouter.LastMessageReceivedUtc == DateTime.MinValue
            ? "--"
            : messageRouter.LastMessageReceivedUtc.ToString("O");
        string sequence = messageRouter == null
            ? "--"
            : $"last={messageRouter.LastSequence} advisory={messageRouter.AdvisoryLastEventSequence}";
        string snapshotTime = messageRouter == null || messageRouter.LastSnapshotHydratedUtc == DateTime.MinValue
            ? "--"
            : messageRouter.LastSnapshotHydratedUtc.ToString("O");
        string snapshot = snapshotHydrator == null
            ? snapshotTime
            : $"{snapshotTime} | v={snapshotHydrator.LastHydratedSnapshotVersion} session={Display(snapshotHydrator.LastHydratedSessionId)} server={Display(snapshotHydrator.LastHydratedServerNowUtc)} player={Display(snapshotHydrator.LastHydratedPlayerId)} tile={Display(snapshotHydrator.LastHydratedTileId)} phase={Display(snapshotHydrator.LastHydratedPhase)} turn={snapshotHydrator.LastHydratedTurnIndex}";
        string error = sessionClient == null ? "" : sessionClient.LastError;
        string errorCode = messageRouter == null ? "" : messageRouter.LastErrorCode;
        if (string.IsNullOrWhiteSpace(error) && messageRouter != null)
        {
            error = messageRouter.LastErrorMessage;
        }

        string fallback = sessionClient != null && sessionClient.IsUsingMockFallback ? " | MOCK FALLBACK ACTIVE" : "";
        string reconnects = sessionClient == null ? "--" : sessionClient.ReconnectAttemptCount.ToString();
        string readOnlyRequests = sessionClient == null ? "--" : sessionClient.ReadOnlyRequestCount.ToString();
        string gameplayRequests = sessionClient == null ? "--" : sessionClient.GameplayCommandRequestCount.ToString();
        string commandInFlight = gameplayCommandDispatcher != null && gameplayCommandDispatcher.IsCommandInFlight
            ? $"{Display(gameplayCommandDispatcher.InFlightRequestType)} id={Display(gameplayCommandDispatcher.InFlightLocalRequestId)}"
            : "none";
        string commandSentAt = gameplayCommandDispatcher == null || gameplayCommandDispatcher.LastCommandSentUtc == DateTime.MinValue
            ? "--"
            : gameplayCommandDispatcher.LastCommandSentUtc.ToString("O");
        string commandLastRequest = gameplayCommandDispatcher == null
            ? "--"
            : $"{Display(gameplayCommandDispatcher.LastCommandRequestType)} id={Display(gameplayCommandDispatcher.LastCommandLocalRequestId)}";
        string commandResult = gameplayCommandDispatcher == null ? "--" : Display(gameplayCommandDispatcher.LastCommandResult);
        string commandError = gameplayCommandDispatcher == null ? "--" : Display(gameplayCommandDispatcher.LastCommandError);
        string commandCount = gameplayCommandDispatcher == null ? "--" : gameplayCommandDispatcher.CommandRequestCount.ToString();

        SetText(modeText, $"Mode: {mode}{fallback}");
        SetText(stateText, $"State: {state} | {bound}");
        SetText(sessionText, $"Session/player: {session} | URL: {url}");
        SetText(lastMessageText, $"Last request: {lastRequest} | Last message: {lastType} @ {lastMessageTime}");
        SetText(commandStatusText, $"Command: last={commandLastRequest} sentAt={commandSentAt} inFlight={commandInFlight} dispatcherCount={commandCount} result={commandResult} error={commandError}");
        SetText(lastSequenceText, $"Sequence: {sequence} | reconnects={reconnects} readOnly={readOnlyRequests} commands={gameplayRequests}");
        SetText(lastSnapshotText, $"Last snapshot: {snapshot}");
        SetText(lastErrorText, $"Last error: code={Display(errorCode)} message={Display(error)}");
        LastRenderedStatus = $"{mode}|{state}|{bound}|{session}|{url}|request={lastRequest}|message={lastType}|command={commandLastRequest}/{commandInFlight}/{commandResult}/{commandError}|sequence={sequence}|snapshot={snapshot}|error={Display(errorCode)}/{Display(error)}|fallback={sessionClient != null && sessionClient.IsUsingMockFallback}";
    }

    private void ApplyRuntimeControlValues()
    {
        if (sessionClient == null)
        {
            return;
        }

        MonoJoeySessionClientMode selectedMode = ModeFromDropdown();
        sessionClient.Configure(
            selectedMode,
            webSocketUrlInput == null ? sessionClient.WebSocketUrl : webSocketUrlInput.text,
            sessionIdInput == null ? sessionClient.SessionId : sessionIdInput.text,
            playerIdInput == null ? sessionClient.PlayerId : playerIdInput.text,
            snapshotHydrator,
            messageRouter,
            sessionClient.RequestSnapshotAfterBroadcastEnabled,
            mockFallbackToggle != null && mockFallbackToggle.isOn,
            sessionClient.EnableMockGameplayCommandTestMode);
    }

    private void HandleConnectClicked()
    {
        ApplyRuntimeControlValues();
        sessionClient?.Connect();
    }

    private void HandleDisconnectClicked()
    {
        sessionClient?.Disconnect();
    }

    private void HandleReconnectClicked()
    {
        ApplyRuntimeControlValues();
        sessionClient?.ReconnectSession();
    }

    private void HandleSnapshotClicked()
    {
        ApplyRuntimeControlValues();
        sessionClient?.RequestSnapshot();
    }

    private void SyncControlsFromClient()
    {
        if (sessionClient == null)
        {
            return;
        }

        if (webSocketUrlInput != null)
        {
            webSocketUrlInput.text = sessionClient.WebSocketUrl;
        }

        if (sessionIdInput != null)
        {
            sessionIdInput.text = sessionClient.SessionId;
        }

        if (playerIdInput != null)
        {
            playerIdInput.text = sessionClient.PlayerId;
        }

        if (modeDropdown != null)
        {
            modeDropdown.value = sessionClient.Mode == MonoJoeySessionClientMode.LiveBackend ? 1 : 0;
        }

        if (mockFallbackToggle != null)
        {
            mockFallbackToggle.isOn = sessionClient.AllowMockFallbackOnLiveFailure;
        }
    }

    private MonoJoeySessionClientMode ModeFromDropdown()
    {
        if (modeDropdown == null || modeDropdown.value == 0)
        {
            return MonoJoeySessionClientMode.MockValidation;
        }

        return MonoJoeySessionClientMode.LiveBackend;
    }

    private void WireControls()
    {
        if (modeDropdown != null && modeDropdown.options.Count == 0)
        {
            modeDropdown.AddOptions(new List<string> { "MockValidation", "LiveBackend" });
        }

        connectButton?.onClick.RemoveListener(HandleConnectClicked);
        disconnectButton?.onClick.RemoveListener(HandleDisconnectClicked);
        reconnectButton?.onClick.RemoveListener(HandleReconnectClicked);
        snapshotButton?.onClick.RemoveListener(HandleSnapshotClicked);
        connectButton?.onClick.AddListener(HandleConnectClicked);
        disconnectButton?.onClick.AddListener(HandleDisconnectClicked);
        reconnectButton?.onClick.AddListener(HandleReconnectClicked);
        snapshotButton?.onClick.AddListener(HandleSnapshotClicked);
    }

    private void EnsureRuntimeControls()
    {
        if (!createRuntimeControls || webSocketUrlInput != null || GetComponentInParent<Canvas>() == null)
        {
            return;
        }

        Font font = ResolveFont();
        GameObject controls = new GameObject("Chunk8LiveConnectionControls", typeof(RectTransform), typeof(VerticalLayoutGroup));
        controls.transform.SetParent(transform, false);
        VerticalLayoutGroup layout = controls.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childForceExpandHeight = false;

        modeDropdown = CreateDropdown(controls.transform, font);
        webSocketUrlInput = CreateInput(controls.transform, font, "ws://127.0.0.1:5000/ws");
        sessionIdInput = CreateInput(controls.transform, font, "sessionId");
        playerIdInput = CreateInput(controls.transform, font, "playerId");
        mockFallbackToggle = CreateToggle(controls.transform, font, "Allow mock fallback");

        GameObject row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(controls.transform, false);
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 4f;
        rowLayout.childForceExpandWidth = false;
        connectButton = CreateButton(row.transform, font, "Connect");
        disconnectButton = CreateButton(row.transform, font, "Disconnect");
        reconnectButton = CreateButton(row.transform, font, "Reconnect");
        snapshotButton = CreateButton(row.transform, font, "Get Snapshot");
    }

    private static InputField CreateInput(Transform parent, Font font, string placeholder)
    {
        GameObject inputObject = new GameObject(placeholder, typeof(RectTransform), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        InputField input = inputObject.GetComponent<InputField>();
        Text text = CreateText(inputObject.transform, font, "");
        Text hint = CreateText(inputObject.transform, font, placeholder);
        hint.color = new Color(0.72f, 0.72f, 0.72f, 1f);
        input.textComponent = text;
        input.placeholder = hint;
        return input;
    }

    private static Dropdown CreateDropdown(Transform parent, Font font)
    {
        GameObject dropdownObject = new GameObject("ModeDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown));
        dropdownObject.transform.SetParent(parent, false);
        dropdownObject.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.10f, 0.90f);
        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.captionText = CreateText(dropdownObject.transform, font, "");
        dropdown.AddOptions(new List<string> { "MockValidation", "LiveBackend" });
        return dropdown;
    }

    private static Toggle CreateToggle(Transform parent, Font font, string label)
    {
        GameObject toggleObject = new GameObject(label, typeof(RectTransform), typeof(Toggle));
        toggleObject.transform.SetParent(parent, false);
        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.graphic = CreateText(toggleObject.transform, font, "x");
        CreateText(toggleObject.transform, font, label);
        return toggle;
    }

    private static Button CreateButton(Transform parent, Font font, string label)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.20f, 1f);
        CreateText(buttonObject.transform, font, label);
        return buttonObject.GetComponent<Button>();
    }

    private static Text CreateText(Transform parent, Font font, string value)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = 14;
        text.color = Color.white;
        text.text = value;
        return text;
    }

    private Font ResolveFont()
    {
        Text existing = GetComponentInChildren<Text>(true);
        if (existing != null && existing.font != null)
        {
            return existing.font;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
