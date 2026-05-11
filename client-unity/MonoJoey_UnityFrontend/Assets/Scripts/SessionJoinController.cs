using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SessionJoinController : MonoBehaviour
{
    private const string DefaultLiveBackendUrl = "ws://127.0.0.1:5000/ws";
    private const string MockModeLabel = "Mock validation";
    private const string LiveModeLabel = "Live backend (/ws)";

    private static bool hasRuntimeMemory;
    private static string rememberedWebSocketUrl = "";
    private static string rememberedSessionId = "";
    private static string rememberedPlayerId = "";
    private static MonoJoeySessionClientMode rememberedMode = MonoJoeySessionClientMode.MockValidation;

    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private MonoJoeyBackendMessageRouter messageRouter;
    [SerializeField] private SnapshotHydrator snapshotHydrator;
    [SerializeField] private LiveSessionContext liveSessionContext;
    [SerializeField] private bool createRuntimeControls = true;
    [SerializeField] private InputField webSocketUrlInput;
    [SerializeField] private InputField sessionIdInput;
    [SerializeField] private InputField playerIdInput;
    [SerializeField] private Dropdown modeDropdown;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button reconnectButton;
    [SerializeField] private Button snapshotButton;
    [SerializeField] private Text statusText;
    [SerializeField] private Text errorText;
    [SerializeField] private Text playerListText;

    private string validationError = "";

    public string LastValidationError => validationError;
    public string LastRenderedStatus { get; private set; } = "";
    public string LastRenderedPlayers { get; private set; } = "";
    public LiveSessionContext Context => liveSessionContext;
    public MonoJoeySessionClient SessionClient => sessionClient;
    public MonoJoeyBackendMessageRouter MessageRouter => messageRouter;
    public SnapshotHydrator SnapshotHydrator => snapshotHydrator;
    public MonoJoeySessionClientMode SelectedBackendMode => SelectedMode();
    public string CurrentBackendUrl => Trimmed(webSocketUrlInput);
    public string CurrentSessionId => Trimmed(sessionIdInput);
    public string CurrentPlayerId => Trimmed(playerIdInput);
    public bool ConnectButtonInteractable => connectButton != null && connectButton.interactable;
    public bool DisconnectButtonInteractable => disconnectButton != null && disconnectButton.interactable;
    public bool ReconnectButtonInteractable => reconnectButton != null && reconnectButton.interactable;
    public bool SnapshotButtonInteractable => snapshotButton != null && snapshotButton.interactable;

    public void Configure(MonoJoeySessionClient client, MonoJoeyBackendMessageRouter router, SnapshotHydrator hydrator)
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= Refresh;
        }

        sessionClient = client;
        messageRouter = router;
        snapshotHydrator = hydrator;
        if (sessionClient != null)
        {
            sessionClient.StatusChanged += Refresh;
        }

        EnsureRuntimeControls();
        EnsureContext();
        WireControls();
        SyncControlsFromClient();
        Refresh();
    }

    public void SetFormValues(string webSocketUrl, string configuredSessionId, string configuredPlayerId, MonoJoeySessionClientMode configuredMode)
    {
        EnsureRuntimeControls();
        EnsureContext();
        WireControls();

        if (modeDropdown != null)
        {
            modeDropdown.value = configuredMode == MonoJoeySessionClientMode.LiveBackend ? 1 : 0;
        }

        string normalizedUrl = configuredMode == MonoJoeySessionClientMode.LiveBackend && string.IsNullOrWhiteSpace(webSocketUrl)
            ? DefaultLiveBackendUrl
            : webSocketUrl ?? "";

        SetInputText(webSocketUrlInput, normalizedUrl);
        SetInputText(sessionIdInput, configuredSessionId ?? "");
        SetInputText(playerIdInput, configuredPlayerId ?? "");
        RememberRuntimeInputs();
        Refresh();
    }

    public void ConnectFromPanel()
    {
        EnsureLiveDefaultForSelectedMode();
        if (!ValidateForm(setError: true))
        {
            Refresh();
            return;
        }

        ApplyFormToClient();
        sessionClient?.Connect();
        validationError = "";
        Refresh();
    }

    public void DisconnectFromPanel()
    {
        sessionClient?.Disconnect();
        validationError = "";
        Refresh();
    }

    public void ReconnectFromPanel()
    {
        EnsureLiveDefaultForSelectedMode();
        if (!ValidateForm(setError: true))
        {
            Refresh();
            return;
        }

        ApplyFormToClient();
        sessionClient?.ReconnectSession();
        validationError = "";
        Refresh();
    }

    public void RequestSnapshotFromPanel()
    {
        EnsureLiveDefaultForSelectedMode();
        if (!ValidateForm(setError: true))
        {
            Refresh();
            return;
        }

        ApplyFormToClient();
        sessionClient?.RequestSnapshot();
        validationError = "";
        Refresh();
    }

    private void Awake()
    {
        EnsureRuntimeControls();
        EnsureContext();
        WireControls();
    }

    private void OnEnable()
    {
        EnsureRuntimeControls();
        EnsureContext();
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
        bool valid = ValidateForm(setError: false);
        bool busy = IsBusyState();
        bool connected = sessionClient != null && sessionClient.IsTransportConnected;
        bool bound = sessionClient != null && sessionClient.IsBoundToIdentity && sessionClient.State == MonoJoeyTransportConnectionState.BoundLive;

        SetInteractable(connectButton, sessionClient != null && valid && !busy && !connected && !bound);
        SetInteractable(disconnectButton, sessionClient != null && (connected || busy || IsDisconnectableState()));
        SetInteractable(reconnectButton, sessionClient != null && valid && connected && !busy);
        SetInteractable(snapshotButton, sessionClient != null && valid && bound && !busy);

        EnsureContext();
        liveSessionContext?.RefreshFrom(
            sessionClient,
            snapshotHydrator,
            SelectedMode(),
            Trimmed(webSocketUrlInput),
            Trimmed(sessionIdInput),
            Trimmed(playerIdInput));

        string lastRequest = sessionClient == null ? "--" : Display(sessionClient.LastSentRequestType);
        string lastMessage = messageRouter == null ? "--" : Display(messageRouter.LastMessageType);
        string lastMessageTime = messageRouter == null || messageRouter.LastMessageReceivedUtc == DateTime.MinValue
            ? "--"
            : messageRouter.LastMessageReceivedUtc.ToString("O");
        string statusSummary = liveSessionContext == null ? "" : liveSessionContext.ConnectionSummary;
        string snapshotSummary = liveSessionContext == null ? "snapshot=--" : liveSessionContext.SnapshotSummary;

        LastRenderedStatus = $"{statusSummary}|request={lastRequest}|message={lastMessage}|{snapshotSummary}";
        LastRenderedPlayers = liveSessionContext == null ? "Players: --" : liveSessionContext.PlayerListSummary;

        SetText(
            statusText,
            $"Connection: {statusSummary}\nLast request: {lastRequest} | Last message: {lastMessage} @ {lastMessageTime}\nSnapshot: {snapshotSummary}");
        SetText(playerListText, $"Players:\n{LastRenderedPlayers}");

        string renderedError = validationError;
        if (string.IsNullOrWhiteSpace(renderedError) && sessionClient != null)
        {
            renderedError = sessionClient.LastError;
        }

        if (string.IsNullOrWhiteSpace(renderedError) && messageRouter != null)
        {
            renderedError = string.IsNullOrWhiteSpace(messageRouter.LastErrorCode)
                ? messageRouter.LastErrorMessage
                : $"{messageRouter.LastErrorCode}: {messageRouter.LastErrorMessage}";
        }

        SetText(errorText, string.IsNullOrWhiteSpace(renderedError) ? "Error: --" : $"Error: {renderedError}");
    }

    private void ApplyFormToClient()
    {
        if (sessionClient == null)
        {
            return;
        }

        RememberRuntimeInputs();
        sessionClient.Configure(
            SelectedMode(),
            Trimmed(webSocketUrlInput),
            Trimmed(sessionIdInput),
            Trimmed(playerIdInput),
            snapshotHydrator,
            messageRouter,
            sessionClient.RequestSnapshotAfterBroadcastEnabled,
            false,
            sessionClient.EnableMockGameplayCommandTestMode);
    }

    private bool ValidateForm(bool setError)
    {
        string url = Trimmed(webSocketUrlInput);
        string session = Trimmed(sessionIdInput);
        string player = Trimmed(playerIdInput);
        string error = "";

        if (string.IsNullOrWhiteSpace(url))
        {
            error = $"Backend URL is required. Use {DefaultLiveBackendUrl} for a local live backend.";
        }
        else if (SelectedMode() == MonoJoeySessionClientMode.LiveBackend
            && !url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            error = "Live backend URL must start with ws:// or wss://.";
        }
        else if (SelectedMode() == MonoJoeySessionClientMode.LiveBackend && !TargetsWebSocketPath(url))
        {
            error = "Live backend URL must target /ws.";
        }
        else if (string.IsNullOrWhiteSpace(session))
        {
            error = "Session ID is required. Paste an existing in-game session ID; this panel does not create sessions.";
        }
        else if (string.IsNullOrWhiteSpace(player))
        {
            error = "Player ID is required. Paste an existing in-game player ID; this panel does not create players.";
        }

        if (setError)
        {
            validationError = error;
        }
        else if (string.IsNullOrWhiteSpace(error) && IsFormError(validationError))
        {
            validationError = "";
        }

        return string.IsNullOrWhiteSpace(error);
    }

    private bool IsBusyState()
    {
        if (sessionClient == null)
        {
            return false;
        }

        return sessionClient.State == MonoJoeyTransportConnectionState.Connecting
            || sessionClient.State == MonoJoeyTransportConnectionState.Reconnecting
            || sessionClient.State == MonoJoeyTransportConnectionState.Hydrating;
    }

    private bool IsDisconnectableState()
    {
        if (sessionClient == null)
        {
            return false;
        }

        return sessionClient.State == MonoJoeyTransportConnectionState.ConnectedUnbound
            || sessionClient.State == MonoJoeyTransportConnectionState.BoundLive
            || sessionClient.State == MonoJoeyTransportConnectionState.Error;
    }

    private void SyncControlsFromClient()
    {
        if (sessionClient != null)
        {
            SetFormValues(sessionClient.WebSocketUrl, sessionClient.SessionId, sessionClient.PlayerId, sessionClient.Mode);
            return;
        }

        if (hasRuntimeMemory)
        {
            SetFormValues(rememberedWebSocketUrl, rememberedSessionId, rememberedPlayerId, rememberedMode);
        }
    }

    private MonoJoeySessionClientMode SelectedMode()
    {
        if (modeDropdown == null || modeDropdown.value == 0)
        {
            return MonoJoeySessionClientMode.MockValidation;
        }

        return MonoJoeySessionClientMode.LiveBackend;
    }

    private void WireControls()
    {
        EnsureModeOptions();
        ConfigureSingleLineInput(webSocketUrlInput);
        ConfigureSingleLineInput(sessionIdInput);
        ConfigureSingleLineInput(playerIdInput);

        connectButton?.onClick.RemoveListener(ConnectFromPanel);
        disconnectButton?.onClick.RemoveListener(DisconnectFromPanel);
        reconnectButton?.onClick.RemoveListener(ReconnectFromPanel);
        snapshotButton?.onClick.RemoveListener(RequestSnapshotFromPanel);
        connectButton?.onClick.AddListener(ConnectFromPanel);
        disconnectButton?.onClick.AddListener(DisconnectFromPanel);
        reconnectButton?.onClick.AddListener(ReconnectFromPanel);
        snapshotButton?.onClick.AddListener(RequestSnapshotFromPanel);

        if (webSocketUrlInput != null)
        {
            webSocketUrlInput.onValueChanged.RemoveListener(HandleFormChanged);
            webSocketUrlInput.onValueChanged.AddListener(HandleFormChanged);
        }

        if (sessionIdInput != null)
        {
            sessionIdInput.onValueChanged.RemoveListener(HandleFormChanged);
            sessionIdInput.onValueChanged.AddListener(HandleFormChanged);
        }

        if (playerIdInput != null)
        {
            playerIdInput.onValueChanged.RemoveListener(HandleFormChanged);
            playerIdInput.onValueChanged.AddListener(HandleFormChanged);
        }

        if (modeDropdown != null)
        {
            modeDropdown.onValueChanged.RemoveListener(HandleModeChanged);
            modeDropdown.onValueChanged.AddListener(HandleModeChanged);
        }
    }

    private void HandleFormChanged(string _)
    {
        RememberRuntimeInputs();
        Refresh();
    }

    private void HandleModeChanged(int _)
    {
        EnsureLiveDefaultForSelectedMode();
        RememberRuntimeInputs();
        Refresh();
    }

    private void EnsureRuntimeControls()
    {
        if (!createRuntimeControls || webSocketUrlInput != null || GetComponentInParent<Canvas>() == null)
        {
            return;
        }

        Font font = ResolveFont();
        GameObject root = new GameObject("SessionJoinPanelControls", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(transform, false);
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.07f, 0.94f);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -16f);
        rect.sizeDelta = new Vector2(520f, 500f);

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 6f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        CreateLabel(root.transform, font, "Live Session Entry");
        modeDropdown = CreateDropdown(root.transform, font);
        webSocketUrlInput = CreateInputRow(root.transform, font, "Backend URL");
        sessionIdInput = CreateInputRow(root.transform, font, "Session ID");
        playerIdInput = CreateInputRow(root.transform, font, "Player ID");

        GameObject row = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(root.transform, false);
        row.GetComponent<LayoutElement>().preferredHeight = 30f;
        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 5f;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        connectButton = CreateButton(row.transform, font, "Connect");
        disconnectButton = CreateButton(row.transform, font, "Disconnect");
        reconnectButton = CreateButton(row.transform, font, "Reconnect");
        snapshotButton = CreateButton(row.transform, font, "Get Snapshot");

        statusText = CreateText(root.transform, font, "Connection: --", 12, TextAnchor.UpperLeft, new Vector2(496f, 92f));
        playerListText = CreateText(root.transform, font, "Players: --", 12, TextAnchor.UpperLeft, new Vector2(496f, 120f));
        errorText = CreateText(root.transform, font, "Error: --", 12, TextAnchor.UpperLeft, new Vector2(496f, 46f));
    }

    private void EnsureContext()
    {
        if (liveSessionContext != null)
        {
            return;
        }

        liveSessionContext = GetComponent<LiveSessionContext>();
        if (liveSessionContext == null)
        {
            liveSessionContext = gameObject.AddComponent<LiveSessionContext>();
        }
    }

    private void EnsureModeOptions()
    {
        if (modeDropdown == null)
        {
            return;
        }

        if (modeDropdown.options.Count == 2
            && modeDropdown.options[0].text == MockModeLabel
            && modeDropdown.options[1].text == LiveModeLabel)
        {
            return;
        }

        int value = modeDropdown.value;
        modeDropdown.ClearOptions();
        modeDropdown.AddOptions(new List<string> { MockModeLabel, LiveModeLabel });
        modeDropdown.value = Mathf.Clamp(value, 0, 1);
        modeDropdown.RefreshShownValue();
    }

    private void EnsureLiveDefaultForSelectedMode()
    {
        if (SelectedMode() == MonoJoeySessionClientMode.LiveBackend && string.IsNullOrWhiteSpace(Trimmed(webSocketUrlInput)))
        {
            SetInputText(webSocketUrlInput, DefaultLiveBackendUrl);
        }
    }

    private void RememberRuntimeInputs()
    {
        hasRuntimeMemory = true;
        rememberedWebSocketUrl = Trimmed(webSocketUrlInput);
        rememberedSessionId = Trimmed(sessionIdInput);
        rememberedPlayerId = Trimmed(playerIdInput);
        rememberedMode = SelectedMode();
    }

    private InputField CreateInputRow(Transform parent, Font font, string placeholder)
    {
        GameObject rowObject = new GameObject($"{placeholder}Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.transform.SetParent(parent, false);
        rowObject.GetComponent<LayoutElement>().preferredHeight = 30f;
        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 5f;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = false;

        InputField input = CreateInput(rowObject.transform, font, placeholder);
        Button copyButton = CreateButton(rowObject.transform, font, "Copy", 44f);
        Button pasteButton = CreateButton(rowObject.transform, font, "Paste", 46f);
        copyButton.onClick.AddListener(() => GUIUtility.systemCopyBuffer = input == null ? "" : input.text ?? "");
        pasteButton.onClick.AddListener(() =>
        {
            if (input != null)
            {
                input.text = GUIUtility.systemCopyBuffer ?? "";
                RememberRuntimeInputs();
                Refresh();
            }
        });

        return input;
    }

    private static void CreateLabel(Transform parent, Font font, string value)
    {
        Text label = CreateText(parent, font, value, 15, TextAnchor.MiddleLeft, new Vector2(496f, 24f));
        label.fontStyle = FontStyle.Bold;
    }

    private static InputField CreateInput(Transform parent, Font font, string placeholder)
    {
        GameObject inputObject = new GameObject(placeholder, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.13f, 1f);
        inputObject.GetComponent<LayoutElement>().preferredHeight = 30f;
        inputObject.GetComponent<LayoutElement>().preferredWidth = 400f;

        InputField input = inputObject.GetComponent<InputField>();
        Text text = CreateText(inputObject.transform, font, "", 13, TextAnchor.MiddleLeft, new Vector2(390f, 30f));
        Text hint = CreateText(inputObject.transform, font, placeholder, 13, TextAnchor.MiddleLeft, new Vector2(390f, 30f));
        hint.color = new Color(0.70f, 0.72f, 0.74f, 1f);
        input.textComponent = text;
        input.placeholder = hint;
        ConfigureSingleLineInput(input);
        return input;
    }

    private static Dropdown CreateDropdown(Transform parent, Font font)
    {
        GameObject dropdownObject = new GameObject("ModeDropdown", typeof(RectTransform), typeof(Image), typeof(Dropdown), typeof(LayoutElement));
        dropdownObject.transform.SetParent(parent, false);
        dropdownObject.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.12f, 1f);
        dropdownObject.GetComponent<LayoutElement>().preferredHeight = 30f;

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.captionText = CreateText(dropdownObject.transform, font, "", 13, TextAnchor.MiddleLeft, new Vector2(490f, 30f));
        dropdown.template = CreateDropdownTemplate(dropdownObject.transform, font);
        dropdown.AddOptions(new List<string> { MockModeLabel, LiveModeLabel });
        return dropdown;
    }

    private static RectTransform CreateDropdownTemplate(Transform parent, Font font)
    {
        GameObject templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        templateObject.transform.SetParent(parent, false);
        templateObject.SetActive(false);
        templateObject.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.10f, 1f);
        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 0f);
        templateRect.anchorMax = new Vector2(1f, 0f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.anchoredPosition = new Vector2(0f, -2f);
        templateRect.sizeDelta = new Vector2(0f, 64f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(templateObject.transform, false);
        viewportObject.GetComponent<Image>().color = Color.white;
        Mask mask = viewportObject.GetComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObject.transform.SetParent(viewportObject.transform, false);
        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandHeight = false;
        contentLayout.childForceExpandWidth = true;
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 60f);

        GameObject itemObject = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        itemObject.transform.SetParent(contentObject.transform, false);
        Image itemImage = itemObject.GetComponent<Image>();
        itemImage.color = new Color(0.13f, 0.15f, 0.17f, 1f);
        itemObject.GetComponent<LayoutElement>().preferredHeight = 30f;
        Text itemText = CreateText(itemObject.transform, font, MockModeLabel, 13, TextAnchor.MiddleLeft, new Vector2(490f, 30f));

        Toggle itemToggle = itemObject.GetComponent<Toggle>();
        itemToggle.targetGraphic = itemImage;
        itemToggle.graphic = null;

        ScrollRect scrollRect = templateObject.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        Dropdown dropdown = parent.GetComponent<Dropdown>();
        if (dropdown != null)
        {
            dropdown.itemText = itemText;
        }

        return templateRect;
    }

    private static Button CreateButton(Transform parent, Font font, string label)
    {
        return CreateButton(parent, font, label, 112f);
    }

    private static Button CreateButton(Transform parent, Font font, string label, float preferredWidth)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.21f, 0.24f, 1f);
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 30f;
        layout.preferredWidth = preferredWidth;
        CreateText(buttonObject.transform, font, label, 12, TextAnchor.MiddleCenter, new Vector2(preferredWidth, 30f));
        return buttonObject.GetComponent<Button>();
    }

    private static Text CreateText(Transform parent, Font font, string value, int fontSize, TextAnchor alignment, Vector2 size)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 0f);
        rect.offsetMax = new Vector2(-6f, 0f);
        rect.sizeDelta = size;

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = size.y;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
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

    private static void ConfigureSingleLineInput(InputField input)
    {
        if (input != null)
        {
            input.lineType = InputField.LineType.SingleLine;
        }
    }

    private static bool TargetsWebSocketPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            return false;
        }

        return string.Equals(uri.AbsolutePath, "/ws", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null)
        {
            selectable.interactable = interactable;
        }
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static void SetInputText(InputField input, string value)
    {
        if (input != null)
        {
            input.text = value ?? "";
        }
    }

    private static string Trimmed(InputField input)
    {
        return input == null ? "" : (input.text ?? "").Trim();
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static bool IsFormError(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && (value.StartsWith("Backend URL is required.", StringComparison.Ordinal)
                || value.StartsWith("Session ID is required.", StringComparison.Ordinal)
                || value.StartsWith("Player ID is required.", StringComparison.Ordinal)
                || value.StartsWith("Live backend URL must", StringComparison.Ordinal));
    }
}
