using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class AgenticTestRunner : MonoBehaviour
{
#if UNITY_EDITOR
    private const bool AutoBootstrapEnabled = true;
    private const string ValidationSceneName = "SampleScene";
    private const string RuntimeRunnerName = "AgenticTestRunner_RuntimeBootstrap";
#endif

    [Header("Player Token Validation")]
    [SerializeField] private GameObject playerTokenPrefab;
    [SerializeField] private Vector3 testTilePosition = new Vector3(2f, 0.5f, 2f);
    [SerializeField] private int testTileIndex = 7;
    [SerializeField] private string testPlayerId = "player-agentic";
    [SerializeField] private Color testTokenColor = new Color(0.95f, 0.70f, 0.18f, 1f);

    [Header("Board Tile Validation")]
    [SerializeField] private GameObject tilePrefab;

    [Header("HUD Validation")]
    [SerializeField] private GameObject hudPrefab;

    [Header("Turn UI Validation")]
    [SerializeField] private GameObject turnUiPrefab;
    [SerializeField] private bool instantiateTurnUi = true;

    [Header("Auction Panel Validation")]
    [SerializeField] private GameObject auctionPanelPrefab;
    [SerializeField] private bool instantiateAuctionPanel = true;

    [Header("Session Join Panel Validation")]
    [SerializeField] private GameObject sessionJoinPanelPrefab;

    [Header("Chunk 9 Optional Live Backend Smoke")]
    [SerializeField] private bool runChunk9LiveBackendSmoke;
    [SerializeField] private bool runChunk9LiveRollSmoke;
    [SerializeField] private string chunk9LiveWebSocketUrl = "ws://127.0.0.1:5000/ws";
    [SerializeField] private string chunk9LiveSessionId = "";
    [SerializeField] private string chunk9LivePlayerId = "";

    [Header("Chunk 11 Optional Live Backend Smoke")]
    [SerializeField] private bool runChunk11LiveBackendSmoke;
    [SerializeField] private string chunk11LiveWebSocketUrl = "ws://127.0.0.1:5000/ws";
    [SerializeField] private string chunk11LiveSessionId = "";
    [SerializeField] private string chunk11LivePlayerId = "";

    [Header("Chunk 13 Optional Live Gameplay Smoke")]
    [SerializeField] private bool runChunk13LiveGameplaySmoke;
    [SerializeField] private string chunk13LiveWebSocketUrl = "ws://127.0.0.1:5000/ws";
    [SerializeField] private string chunk13LiveSessionId = "";
    [SerializeField] private string chunk13LivePlayerId = "";
    [SerializeField] private bool chunk13AllowAuctionBid;
    [SerializeField] private int chunk13AuctionBidAmount;
    [SerializeField] private float chunk13LiveTimeoutSeconds = 10f;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapForSampleScene()
    {
        if (!AutoBootstrapEnabled || SceneManager.GetActiveScene().name != ValidationSceneName)
        {
            return;
        }

        if (FindAnyObjectByType<AgenticTestRunner>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject runner = new GameObject(RuntimeRunnerName, typeof(AgenticTestRunner))
        {
            hideFlags = HideFlags.DontSave
        };

        if (Application.isPlaying)
        {
            DontDestroyOnLoad(runner);
        }

        Debug.Log("[AgenticTestRunner] Runtime validation runner auto-bootstrapped for SampleScene. Scene files do not need saved runner objects.", runner);
    }
#endif

    private void Start()
    {
        EnsureEventSystem();
        Dictionary<string, BoardTileController> tilesById = ValidateBoardTiles();
        StartCoroutine(RunFullMockTurnValidation(tilesById));
        ValidateAuctionPanel();
    }

    private Dictionary<string, BoardTileController> ValidateBoardTiles()
    {
        Dictionary<string, BoardTileController> tilesById = new Dictionary<string, BoardTileController>();
        GameObject prefab = tilePrefab != null ? tilePrefab : LoadPrefabInEditor("Assets/Prefabs/TilePrefab.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] TilePrefab is not assigned.");
            return tilesById;
        }

        string[] tileIds = { "start", "property_01", "property_02", "auction_test" };
        Vector3[] positions =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            new Vector3(6f, 0f, 0f),
        };

        for (int i = 0; i < tileIds.Length; i++)
        {
            GameObject tileObject = Instantiate(prefab, positions[i], Quaternion.identity);
            tileObject.name = $"Tile_{tileIds[i]}";

            BoardTileController tile = tileObject.GetComponent<BoardTileController>();
            if (tile == null)
            {
                Debug.LogError("[AgenticTestRunner] TilePrefab is missing BoardTileController.", tileObject);
                continue;
            }

            string ownerId = i == 1 ? "player-2" : i == 2 ? "player-3" : "";
            Color ownerColor = i == 1 ? new Color(0.20f, 0.55f, 0.85f, 1f) : new Color(0.95f, 0.70f, 0.18f, 1f);
            tile.BindTile(tileIds[i], ownerId, ownerColor);
            tile.SetBoardIndex(i);
            tile.SetHighlighted(i == 3);
            tilesById[tile.TileId] = tile;

            Debug.Log($"[AgenticTestRunner] Tile bound: tileId={tile.TileId}, ownerId={tile.OwnerPlayerId}, highlighted={tile.IsHighlighted}.", tileObject);
        }

        return tilesById;
    }

    private IEnumerator RunFullMockTurnValidation(Dictionary<string, BoardTileController> tilesById)
    {
        GameObject hudObject = InstantiateRequiredPrefab(hudPrefab, "Assets/Prefabs/HUDPrefab.prefab", "HUDPrefab");
        GameObject turnObject = InstantiateRequiredPrefab(turnUiPrefab, "Assets/Prefabs/TurnUIPrefab.prefab", "TurnUIPrefab");
        GameObject tokenObject = InstantiateRequiredPrefab(playerTokenPrefab, "Assets/Prefabs/PlayerToken.prefab", "PlayerToken");

        if (hudObject == null || turnObject == null || tokenObject == null)
        {
            yield break;
        }

        HUDController hud = hudObject.GetComponentInChildren<HUDController>();
        TurnController turnController = turnObject.GetComponentInChildren<TurnController>();
        DiceAnimator diceAnimator = turnObject.GetComponentInChildren<DiceAnimator>();
        PlayerTokenController token = tokenObject.GetComponent<PlayerTokenController>();
        TokenAnimator tokenAnimator = tokenObject.GetComponent<TokenAnimator>();
        if (tokenAnimator == null)
        {
            tokenAnimator = tokenObject.AddComponent<TokenAnimator>();
        }

        if (hud == null || turnController == null || token == null || tokenAnimator == null)
        {
            Debug.LogError($"[AgenticTestRunner] Full mock turn missing components: hud={hud != null}, turn={turnController != null}, token={token != null}, tokenAnimator={tokenAnimator != null}. Mock/read-only; no backend mutation.", this);
            yield break;
        }

        BoardTileController[] boardPath = OrderedBoardPath(tilesById);
        if (boardPath.Length == 0)
        {
            Debug.LogWarning("[AgenticTestRunner] Full mock turn skipped because no board tiles were available. Mock/read-only; no backend mutation.", this);
            yield break;
        }

        token.SetPlayer(testPlayerId, testTokenColor);
        token.SetCurrentTileIndex(0);
        token.MoveToTilePosition(boardPath[0].GetTokenAnchorPosition(0.35f));

        HUDController.PlayerHudSnapshot player = new HUDController.PlayerHudSnapshot(
            testPlayerId,
            "Agentic Player",
            "gold",
            1500,
            "start",
            120,
            10f,
            12,
            "starter",
            false,
            false,
            false);
        HUDController.TurnHudSnapshot turn = new HUDController.TurnHudSnapshot(
            testPlayerId,
            8,
            "AwaitingRoll",
            false,
            false,
            false);

        turnController.ConfigureMockTurnReferences(hud, tokenAnimator, boardPath, testPlayerId);
        turnController.BindHudSnapshot(player, turn);
        turnController.StartTurn();

        Debug.Log($"[AgenticTestRunner] Chunk 4 full mock turn start: player={player.PlayerId}, tile={player.CurrentTileId}, turn={turn.TurnIndex}/{turn.Phase}, rollButtonInteractable={turnController.RollButtonInteractable}. Mock/read-only; no backend mutation.", turnObject);
        turnController.RollDice();
        Debug.Log($"[AgenticTestRunner] Chunk 4 roll triggered: rollButtonInteractable={turnController.RollButtonInteractable}, diceValues={string.Join(" + ", turnController.LastRollValues)}, diceAnimating={(diceAnimator != null && diceAnimator.IsAnimating)}, tokenAnimating={tokenAnimator.IsAnimating}. Mock/read-only; no backend mutation.", turnObject);

        while (turnController.IsRolling || (diceAnimator != null && diceAnimator.IsAnimating) || tokenAnimator.IsAnimating)
        {
            yield return null;
        }

        Debug.Log($"[AgenticTestRunner] Chunk 4 roll complete: rollButtonInteractable={turnController.RollButtonInteractable}, diceValues={string.Join(" + ", turnController.LastRollValues)}, diceAnimating={(diceAnimator != null && diceAnimator.IsAnimating)}. Mock/read-only; no backend mutation.", turnObject);
        Debug.Log($"[AgenticTestRunner] Chunk 4 token path: path={string.Join(" -> ", turnController.LastMovementPath)}, finalTile={tokenAnimator.LastCompletedTileId}, finalBoardIndex={tokenAnimator.FinalTileIndex}, elapsedSteps={tokenAnimator.ElapsedStepCount}, finalPosition={turnController.LastTokenPosition}. Mock/read-only; no backend mutation.", tokenObject);
        Debug.Log($"[AgenticTestRunner] Chunk 4 HUD updated: money={turnController.LastHudPlayerSnapshot.Money}, loan={turnController.LastHudPlayerSnapshot.LoanTotalBorrowed}, currentTile={turnController.LastHudPlayerSnapshot.CurrentTileId}, hasRolled={turnController.LastSnapshot.HasRolledThisTurn}, phase={turnController.LastSnapshot.Phase}. Mock/read-only; no backend mutation.", hudObject);
        Debug.Log($"[AgenticTestRunner] Chunk 4 turn debug log:\n{turnController.DebugLogText}", turnObject);

        turnController.EndTurn();
        Debug.Log($"[AgenticTestRunner] Chunk 4 full mock turn end: rollButtonInteractable={turnController.RollButtonInteractable}, turnActive={turnController.IsTurnActive}. Mock/read-only; no backend mutation.", turnObject);

        ValidatePlayerTokenSummary(tokenObject, token, tokenAnimator);
        ValidateHudSummary(hudObject, hud);
        ValidateTurnUiSummary(turnObject, turnController, diceAnimator);

        yield return RunReadOnlySnapshotHydrationValidation(hud, turnController, token, tokenAnimator, boardPath);
    }

    private static GameObject InstantiateRequiredPrefab(GameObject assignedPrefab, string editorAssetPath, string label)
    {
        GameObject prefab = assignedPrefab != null ? assignedPrefab : LoadPrefabInEditor(editorAssetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[AgenticTestRunner] {label} is not assigned and could not be loaded. Mock/read-only; no backend mutation.");
            return null;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = $"{label}_Chunk4Runtime";
        return instance;
    }

    private static BoardTileController[] OrderedBoardPath(IReadOnlyDictionary<string, BoardTileController> tilesById)
    {
        if (tilesById == null || tilesById.Count == 0)
        {
            return new BoardTileController[0];
        }

        string[] pathIds = { "start", "property_01", "property_02", "auction_test" };
        List<BoardTileController> path = new List<BoardTileController>();
        for (int i = 0; i < pathIds.Length; i++)
        {
            if (tilesById.TryGetValue(pathIds[i], out BoardTileController tile) && tile != null)
            {
                path.Add(tile);
            }
        }

        return path.ToArray();
    }

    private static void ValidatePlayerTokenSummary(GameObject tokenObject, PlayerTokenController token, TokenAnimator tokenAnimator)
    {
        Debug.Log($"[AgenticTestRunner] PlayerToken validation summary: playerId={token.PlayerId}, currentTileIndex={token.CurrentTileIndex}, color={token.TokenColor}, tag={tokenObject.tag}, path={string.Join(" -> ", tokenAnimator.LastPathTileIds)}, finalTile={tokenAnimator.LastCompletedTileId}. Mock/read-only; no backend mutation.", tokenObject);
        Debug.Log(tokenObject.CompareTag("PlayerToken")
            ? "[AgenticTestRunner] Tag validation reports PlayerToken. Mock/read-only; no backend mutation."
            : $"[AgenticTestRunner] Tag validation failed: {tokenObject.tag}. Mock/read-only; no backend mutation.", tokenObject);
    }

    private static void ValidateHudSummary(GameObject hudObject, HUDController hud)
    {
        Debug.Log($"[AgenticTestRunner] HUD validation summary: player={hud.LastPlayerSnapshot.PlayerId}, money={hud.LastPlayerSnapshot.Money}, loan={hud.LastPlayerSnapshot.LoanTotalBorrowed}, turn={hud.LastTurnSnapshot.TurnIndex}/{hud.LastTurnSnapshot.Phase}, hasRolled={hud.LastTurnSnapshot.HasRolledThisTurn}. Mock/read-only; no backend mutation.", hudObject);
    }

    private static void ValidateTurnUiSummary(GameObject turnObject, TurnController turnController, DiceAnimator diceAnimator)
    {
        Debug.Log($"[AgenticTestRunner] Turn UI inspector fields: turnController={turnController != null}, diceImages={turnController.DiceImageCount}, rollButtonInteractable={turnController.RollButtonInteractable}, diceAnimator={diceAnimator != null}, diceFaces={(diceAnimator == null ? 0 : diceAnimator.DiceFaceCount)}, rollDuration={(diceAnimator == null ? 0f : diceAnimator.RollDuration)}. Mock/read-only; no backend mutation.", turnObject);
    }

    private IEnumerator RunReadOnlySnapshotHydrationValidation(
        HUDController hud,
        TurnController turnController,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        BoardTileController[] boardPath)
    {
        GameObject auctionObject = InstantiateRequiredPrefab(auctionPanelPrefab, "Assets/Prefabs/AuctionPanel.prefab", "AuctionPanel");
        if (auctionObject == null)
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 5 snapshot hydration skipped because AuctionPanel prefab was unavailable. Read-only snapshot hydration; no backend mutation.", this);
            yield break;
        }

        AuctionPanelController auction = auctionObject.GetComponentInChildren<AuctionPanelController>();
        if (auction == null)
        {
            Debug.LogError("[AgenticTestRunner] Chunk 5 snapshot hydration skipped because AuctionPanelController was missing. Read-only snapshot hydration; no backend mutation.", auctionObject);
            yield break;
        }

        GameObject hydratorObject = new GameObject("SnapshotHydrator_Chunk5Runtime", typeof(SnapshotHydrator));
        SnapshotHydrator hydrator = hydratorObject.GetComponent<SnapshotHydrator>();
        hydrator.Configure(hud, turnController, auction, token, tokenAnimator, boardPath, testPlayerId);
        SubscribeToChunk6HydratorHooks(hydrator, hydratorObject);

        bool firstHydrated = hydrator.HydrateSnapshotJson(Chunk5SnapshotJson(activeAuction: true));
        BoardTileController propertyOne = FindBoardTile(boardPath, "property_01");
        Debug.Log($"[AgenticTestRunner] Chunk 5 first snapshot hydrated={firstHydrated}: hudMoney={hud.LastPlayerSnapshot.Money}, hudLoan={hud.LastPlayerSnapshot.LoanTotalBorrowed}, hudTile={hud.LastPlayerSnapshot.CurrentTileId}, turnPhase={turnController.LastSnapshot.Phase}, hasRolled={turnController.LastSnapshot.HasRolledThisTurn}, hasResolved={turnController.LastSnapshot.HasResolvedTileThisTurn}, auctionHighBidder={auction.HighBidderPlayerId}, auctionHighBid={auction.CurrentHighBid}, tileOwner={propertyOne?.OwnerPlayerId}, tileIndex={(propertyOne == null ? -1 : propertyOne.BoardIndex)}, tokenPlayer={token.PlayerId}, tokenTileIndex={token.CurrentTileIndex}, tokenPosition={token.transform.position}. Read-only snapshot hydration; no backend mutation.", hydratorObject);

        bool secondHydrated = hydrator.HydrateSnapshotJson(Chunk5SnapshotJson(activeAuction: false));
        BoardTileController propertyTwo = FindBoardTile(boardPath, "property_02");
        Debug.Log($"[AgenticTestRunner] Chunk 5 second snapshot hydrated={secondHydrated}: auctionId={auction.AuctionId}, auctionHighBidder={auction.HighBidderPlayerId}, auctionHighBid={auction.CurrentHighBid}, helpers movement={(hydrator.LastMovement == null ? "cleared" : "present")}, moneyDeltas={hydrator.LastMoneyDeltas.Length}, ownershipChanges={hydrator.LastPropertyOwnershipChanges.Length}, eliminations={hydrator.LastPlayerEliminations.Length}, tileOwner={propertyTwo?.OwnerPlayerId}, tileIndex={(propertyTwo == null ? -1 : propertyTwo.BoardIndex)}, tokenPlayer={token.PlayerId}, tokenTileId={hydrator.LastHydratedTileId}, tokenTileIndex={token.CurrentTileIndex}, tokenPosition={token.transform.position}. Read-only snapshot hydration; no backend mutation.", hydratorObject);

        string beforeLiveMoneyLoan = $"{hud.LastPlayerSnapshot.Money}/{hud.LastPlayerSnapshot.LoanTotalBorrowed}";
        string beforeLivePhase = turnController.LastSnapshot.Phase;
        string beforeLiveAuction = $"{auction.HighBidderPlayerId}/{auction.CurrentHighBid}";
        string beforeLiveToken = $"{hydrator.LastHydratedTileId}/{token.CurrentTileIndex}/{token.transform.position}";
        bool liveHydrated = hydrator.RefreshFromLiveSessionSnapshotJson(Chunk6LiveSessionUpdateSnapshotJson());
        BoardTileController auctionTile = FindBoardTile(boardPath, "auction_test");
        Debug.Log($"[AgenticTestRunner] {UtcNowStamp()} Chunk 6 mock session update hydrated={liveHydrated}: hudMoneyLoan {beforeLiveMoneyLoan}->{hud.LastPlayerSnapshot.Money}/{hud.LastPlayerSnapshot.LoanTotalBorrowed}, turnPhase {beforeLivePhase}->{turnController.LastSnapshot.Phase}, hasRolled={turnController.LastSnapshot.HasRolledThisTurn}, hasResolved={turnController.LastSnapshot.HasResolvedTileThisTurn}, token {beforeLiveToken}->{hydrator.LastHydratedTileId}/{token.CurrentTileIndex}/{token.transform.position}, auction {beforeLiveAuction}->{auction.HighBidderPlayerId}/{auction.CurrentHighBid}, auctionTileOwner={auctionTile?.OwnerPlayerId}, auctionTileHighlighted={(auctionTile != null && auctionTile.IsHighlighted)}. Read-only mock live session update; no backend mutation.", hydratorObject);

        yield return RunChunk9ReadOnlyTransportValidation(hydrator);
        yield return RunChunk9GameplayCommandDispatcherValidation(hydrator, hud, turnController, auction, token);
        yield return RunChunk10PlayableTurnUiCommandFlow(hydrator, hud, turnController, auction, token, tokenAnimator, boardPath);
        yield return RunChunk12GameplayCommandFeedbackValidation(hydrator, hud, turnController, auction, token, tokenAnimator, boardPath);
        yield return RunChunk16PlayableUserFlowPolishValidation(hydrator, hud, turnController, auction, token, tokenAnimator, boardPath);
        yield return RunChunk11SessionJoinPanelValidation(hydrator);
        yield return RunChunk14LiveSessionEntryValidation(hydrator);
        yield return RunChunk15LiveSmokeExecutionHelperValidation(hydrator);
        yield return RunChunk17BoardLayoutValidation(hydrator, token, tokenAnimator);
        yield return RunOptionalChunk11LiveBackendSmoke(hydrator);
        yield return RunOptionalChunk9LiveBackendSmoke(hydrator);
        yield return RunOptionalChunk13LiveGameplaySmoke(hydrator, hud, turnController, auction, token, tokenAnimator, boardPath);
    }

    private IEnumerator RunChunk9ReadOnlyTransportValidation(SnapshotHydrator hydrator)
    {
        GameObject missingIdentityObject = new GameObject(
            "MonoJoeyBackendTransport_Chunk8MissingIdentityRuntime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport missingIdentityTransport = missingIdentityObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter missingIdentityRouter = missingIdentityObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient missingIdentitySession = missingIdentityObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyConnectionStatusController missingIdentityStatus = missingIdentityObject.GetComponent<MonoJoeyConnectionStatusController>();
        missingIdentitySession.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "",
            "",
            hydrator,
            missingIdentityRouter,
            true,
            false,
            false);
        missingIdentityStatus.Configure(missingIdentitySession, missingIdentityRouter);
        missingIdentitySession.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 missing session/player connect: state={missingIdentitySession.State}, bound={missingIdentitySession.IsBoundToIdentity}, sentRequests={missingIdentityTransport.SentRequestTypes.Count}. Expected connected unbound with no request.", missingIdentityObject);
        missingIdentitySession.RequestSnapshot();
        Debug.Log($"[AgenticTestRunner] Chunk 8 missing identity get_snapshot skipped: sentRequests={missingIdentityTransport.SentRequestTypes.Count}, lastRequest={DisplayLogValue(missingIdentitySession.LastSentRequestType)}. Read-only backend transport; no gameplay mutation request.", missingIdentityObject);
        missingIdentitySession.Disconnect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 missing identity disconnect: state={missingIdentitySession.State}.", missingIdentityObject);

        GameObject chunk8Object = new GameObject(
            "MonoJoeyBackendTransport_Chunk8Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = chunk8Object.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = chunk8Object.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = chunk8Object.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = chunk8Object.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = chunk8Object.GetComponent<MonoJoeyConnectionStatusController>();
        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "session_chunk_8",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            false);
        status.Configure(session, router);
        dispatcher.Configure(session);
        Debug.Log($"[AgenticTestRunner] Chunk 9 mode switching default: mode={session.Mode}, mockCommandTestMode={session.EnableMockGameplayCommandTestMode}, fallback={session.AllowMockFallbackOnLiveFailure}. Mock validation remains default.", chunk8Object);

        session.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 mock transport connected/reconnect hydrated: state={session.State}, bound={session.IsBoundToIdentity}, reconnectHydrated={router.ReconnectResultCount}, reconnects={session.ReconnectAttemptCount}, advisorySequence={router.AdvisoryLastEventSequence}, hydratorSource={hydrator.LastHydrationSourceMessageType}. Read-only backend transport; no gameplay mutation request.", chunk8Object);
        Debug.Log($"[AgenticTestRunner] Chunk 8 reconnect observability: snapshotVersion={hydrator.LastHydratedSnapshotVersion}, session={DisplayLogValue(hydrator.LastHydratedSessionId)}, serverNowUtc={DisplayLogValue(hydrator.LastHydratedServerNowUtc)}, player={DisplayLogValue(hydrator.LastHydratedPlayerId)}, tile={DisplayLogValue(hydrator.LastHydratedTileId)}, phase={DisplayLogValue(hydrator.LastHydratedPhase)}, turn={hydrator.LastHydratedTurnIndex}.", chunk8Object);

        session.RequestSnapshot();
        Debug.Log($"[AgenticTestRunner] Chunk 8 manual snapshot_result hydrated after reconnect: snapshotHydrated={router.SnapshotResultCount}, hydratorSource={hydrator.LastHydrationSourceMessageType}, player={hydrator.LastHydratedPlayerId}, tile={hydrator.LastHydratedTileId}, phase={hydrator.LastHydratedPhase}. Read-only backend transport; no gameplay mutation request.", chunk8Object);

        mockTransport.EmitCannedError();
        Debug.Log($"[AgenticTestRunner] Chunk 8 backend error envelope logged: errors={router.ErrorEnvelopeCount}, code={DisplayLogValue(router.LastErrorCode)}, message={DisplayLogValue(router.LastErrorMessage)}. Read-only backend transport; no local gameplay compensation.", chunk8Object);

        mockTransport.EmitIgnoredBroadcast(9);
        mockTransport.EmitIgnoredBroadcast(7);
        mockTransport.EmitUnknownMessage();
        yield return new WaitForSeconds(0.75f);

        status.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 8 stale/out-of-order sequenced broadcasts ignored/read-only refresh: broadcasts={router.SequencedBroadcastCount}, stale={router.StaleSequenceCount}, outOfOrder={router.OutOfOrderSequenceCount}, snapshotRequests={session.ReadOnlyRequestCount}, lastSequence={router.LastSequence}. No incremental gameplay application.", chunk8Object);
        Debug.Log($"[AgenticTestRunner] Chunk 8 connection status updated: {status.LastRenderedStatus}. Read-only backend transport; no gameplay mutation request.", chunk8Object);

        bool defaultCommandSent = dispatcher.TryRollDice();
        Debug.Log($"[AgenticTestRunner] Chunk 9 default gameplay command guard: sent={defaultCommandSent}, dispatcherRequests={dispatcher.CommandRequestCount}, sessionCommands={session.GameplayCommandRequestCount}, mockMutationRequests={mockTransport.GameplayMutationRequestCount}. Expected zero mutation requests in normal MockValidation.", chunk8Object);

        session.Disconnect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 disconnect transition: state={session.State}, bound={session.IsBoundToIdentity}.", chunk8Object);
        session.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 reconnect transition: state={session.State}, bound={session.IsBoundToIdentity}, reconnects={session.ReconnectAttemptCount}, reconnectHydrated={router.ReconnectResultCount}.", chunk8Object);
        Debug.Log(mockTransport.GameplayMutationRequestCount == 0
            ? $"[AgenticTestRunner] Chunk 8 no gameplay mutation request sent. Sent read-only types={string.Join(", ", mockTransport.SentRequestTypes)}."
            : $"[AgenticTestRunner] Chunk 8 validation failed: gameplay mutation requests sent={mockTransport.GameplayMutationRequestCount}.", chunk8Object);
    }

    private IEnumerator RunChunk9GameplayCommandDispatcherValidation(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token)
    {
        GameObject commandObject = new GameObject(
            "MonoJoeyGameplayCommands_Chunk9Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = commandObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = commandObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = commandObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = commandObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = commandObject.GetComponent<MonoJoeyConnectionStatusController>();
        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "session_chunk_9",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            true);
        dispatcher.Configure(session);
        status.Configure(session, router);

        bool blockedDisconnected = dispatcher.TryRollDice();
        Debug.Log($"[AgenticTestRunner] Chunk 9 disconnected dispatcher guard: sent={blockedDisconnected}, error={DisplayLogValue(dispatcher.LastCommandError)}.", commandObject);

        session.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 9 mock command test connect: state={session.State}, bound={session.IsBoundToIdentity}, mockCommandResponses={mockTransport.EnableGameplayCommandTestResponses}.", commandObject);

        string beforeDirectState = $"{hud.LastPlayerSnapshot.Money}/{turnController.LastSnapshot.Phase}/{auction.CurrentHighBid}/{token.CurrentTileIndex}";
        dispatcher.TryRollDice();
        dispatcher.TryResolveTile();
        dispatcher.TryExecuteTile();
        dispatcher.TryEndTurn();
        dispatcher.TryPlaceBid(220);
        bool blockedBadBid = dispatcher.TryPlaceBid(0);
        bool allowedUnknown = dispatcher.CanSendCommand("take_loan", out string unknownReason);
        string afterDirectState = $"{hud.LastPlayerSnapshot.Money}/{turnController.LastSnapshot.Phase}/{auction.CurrentHighBid}/{token.CurrentTileIndex}";

        Debug.Log($"[AgenticTestRunner] Chunk 9 command envelopes sent: count={dispatcher.CommandRequestCount}, sessionCommands={session.GameplayCommandRequestCount}, directResults={router.DirectCommandResultCount}, sentTypes={string.Join(", ", mockTransport.SentRequestTypes)}.", commandObject);
        Debug.Log($"[AgenticTestRunner] Chunk 9 last place_bid JSON: {LastSentMessageForType(mockTransport, MonoJoeyTransportMessageTypes.PlaceBid)}", commandObject);
        Debug.Log($"[AgenticTestRunner] Chunk 9 command guards: badBidSent={blockedBadBid}, unknownAllowed={allowedUnknown}, unknownReason={DisplayLogValue(unknownReason)}.", commandObject);
        Debug.Log($"[AgenticTestRunner] Chunk 9 direct results did not mutate UI before snapshot: before={beforeDirectState}, after={afterDirectState}.", commandObject);

        mockTransport.EmitCannedSnapshotResult();
        Debug.Log($"[AgenticTestRunner] Chunk 9 snapshot after command path hydrated: snapshots={router.SnapshotResultCount}, player={DisplayLogValue(hydrator.LastHydratedPlayerId)}, tile={DisplayLogValue(hydrator.LastHydratedTileId)}, phase={DisplayLogValue(hydrator.LastHydratedPhase)}.", commandObject);

        mockTransport.EmitCannedError();
        status.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 9 backend error status: inFlight={dispatcher.IsCommandInFlight}, error={DisplayLogValue(dispatcher.LastCommandError)}, rendered={status.LastRenderedStatus}.", commandObject);
        yield return null;
    }

    private IEnumerator RunChunk10PlayableTurnUiCommandFlow(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        BoardTileController[] boardPath)
    {
        GameObject commandObject = new GameObject(
            "MonoJoeyPlayableTurnUi_Chunk10Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = commandObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = commandObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = commandObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = commandObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = commandObject.GetComponent<MonoJoeyConnectionStatusController>();

        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "session_chunk_10",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            true);
        dispatcher.Configure(session);
        status.Configure(session, router);
        turnController.ConfigureLiveGameplayCommandDispatcher(dispatcher, testPlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(dispatcher);

        session.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 10 mock-live command flow connected: state={session.State}, bound={session.IsBoundToIdentity}, dispatcherTestMode={session.EnableMockGameplayCommandTestMode}, mockCommandResponses={mockTransport.EnableGameplayCommandTestResponses}.", commandObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", testPlayerId, 20, false, false, false, 1500, "start", false, 0, ""));
        LogChunk10UiState("initial authoritative snapshot", commandObject, hud, turnController, auction, token, tokenAnimator, hydrator, dispatcher, mockTransport, boardPath);

        string beforeRollDirect = Chunk10StateSignature(hud, turnController, auction, token);
        turnController.RollDice();
        string afterRollDirect = Chunk10StateSignature(hud, turnController, auction, token);
        Debug.Log($"[AgenticTestRunner] Chunk 10 roll_dice clicked: requestType={DisplayLogValue(dispatcher.LastCommandRequestType)}, localRequestId={DisplayLogValue(dispatcher.LastCommandLocalRequestId)}, directResultType={DisplayLogValue(router.LastDirectCommandResultType)}, inFlight={dispatcher.IsCommandInFlight}, inFlightLog={DisplayLogValue(dispatcher.LastInFlightStateLog)}, uiBefore={beforeRollDirect}, uiAfter={afterRollDirect}. Direct result clears in-flight only; no snapshot mutation.", commandObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_tile_resolution", testPlayerId, 20, true, false, false, 1488, "property_01", false, 0, ""));
        LogChunk10UiState("post-roll snapshot_result", commandObject, hud, turnController, auction, token, tokenAnimator, hydrator, dispatcher, mockTransport, boardPath);

        turnController.ResolveTile();
        Debug.Log($"[AgenticTestRunner] Chunk 10 resolve_tile clicked: requestType={DisplayLogValue(dispatcher.LastCommandRequestType)}, localRequestId={DisplayLogValue(dispatcher.LastCommandLocalRequestId)}, directResultType={DisplayLogValue(router.LastDirectCommandResultType)}, inFlight={dispatcher.IsCommandInFlight}, blocked={DisplayLogValue(turnController.LastResolveBlockedReason)}.", commandObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("auction_bidding", testPlayerId, 20, true, true, false, 1488, "auction_test", true, 240, "player-2"));
        auction.SetBidInputTextForValidation("260");
        LogChunk10UiState("post-resolve active-auction snapshot_result", commandObject, hud, turnController, auction, token, tokenAnimator, hydrator, dispatcher, mockTransport, boardPath);

        auction.SubmitLocalBidRequest();
        Debug.Log($"[AgenticTestRunner] Chunk 10 place_bid clicked during active auction: bidButton={auction.BidButtonInteractable}, blocked={DisplayLogValue(auction.LastBidBlockedReason)}, requestType={DisplayLogValue(dispatcher.LastCommandRequestType)}, localRequestId={DisplayLogValue(dispatcher.LastCommandLocalRequestId)}, directResultType={DisplayLogValue(router.LastDirectCommandResultType)}, inFlight={dispatcher.IsCommandInFlight}.", commandObject);

        turnController.ExecuteTile();
        Debug.Log($"[AgenticTestRunner] Chunk 10 execute_tile clicked: requestType={DisplayLogValue(dispatcher.LastCommandRequestType)}, localRequestId={DisplayLogValue(dispatcher.LastCommandLocalRequestId)}, directResultType={DisplayLogValue(router.LastDirectCommandResultType)}, inFlight={dispatcher.IsCommandInFlight}, blocked={DisplayLogValue(turnController.LastExecuteBlockedReason)}.", commandObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_end_turn", testPlayerId, 20, true, true, true, 1460, "auction_test", false, 0, testPlayerId));
        LogChunk10UiState("post-execute no-auction snapshot_result", commandObject, hud, turnController, auction, token, tokenAnimator, hydrator, dispatcher, mockTransport, boardPath);

        turnController.EndTurn();
        Debug.Log($"[AgenticTestRunner] Chunk 10 end_turn clicked: requestType={DisplayLogValue(dispatcher.LastCommandRequestType)}, localRequestId={DisplayLogValue(dispatcher.LastCommandLocalRequestId)}, directResultType={DisplayLogValue(router.LastDirectCommandResultType)}, inFlight={dispatcher.IsCommandInFlight}, blocked={DisplayLogValue(turnController.LastEndTurnBlockedReason)}.", commandObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", "player-2", 21, false, false, false, 1460, "auction_test", false, 0, testPlayerId));
        LogChunk10UiState("next-turn snapshot_result", commandObject, hud, turnController, auction, token, tokenAnimator, hydrator, dispatcher, mockTransport, boardPath);
        Debug.Log($"[AgenticTestRunner] Chunk 10 final local-turn buttons disabled: roll={turnController.RollButtonInteractable}, resolve={turnController.ResolveButtonInteractable}, execute={turnController.ExecuteButtonInteractable}, end={turnController.EndTurnButtonInteractable}, rollBlocked={DisplayLogValue(turnController.LastRollBlockedReason)}.", commandObject);

        yield return null;
    }

    private IEnumerator RunChunk12GameplayCommandFeedbackValidation(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        BoardTileController[] boardPath)
    {
        GameObject feedbackObject = new GameObject(
            "MonoJoeyPlayableCommandFeedback_Chunk12Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = feedbackObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = feedbackObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = feedbackObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = feedbackObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = feedbackObject.GetComponent<MonoJoeyConnectionStatusController>();

        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "session_chunk_12",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            true);
        dispatcher.Configure(session);
        status.Configure(session, router);
        turnController.ConfigureLiveGameplayCommandDispatcher(dispatcher, testPlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(dispatcher);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", testPlayerId, 30, false, false, false, 1500, "start", false, 0, ""));
        LogChunk12Feedback("no-connection blocked turn buttons", feedbackObject, status, turnController, auction, dispatcher);

        GameObject unboundObject = new GameObject(
            "MonoJoeyPlayableCommandFeedback_Chunk12UnboundRuntime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };
        MonoJoeyBackendMessageRouter unboundRouter = unboundObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient unboundSession = unboundObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher unboundDispatcher = unboundObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController unboundStatus = unboundObject.GetComponent<MonoJoeyConnectionStatusController>();
        unboundSession.Configure(MonoJoeySessionClientMode.MockValidation, "", "", "", hydrator, unboundRouter, true, false, true);
        unboundDispatcher.Configure(unboundSession);
        unboundStatus.Configure(unboundSession, unboundRouter);
        turnController.ConfigureLiveGameplayCommandDispatcher(unboundDispatcher, testPlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(unboundDispatcher);
        unboundSession.Connect();
        unboundRouter.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", testPlayerId, 31, false, false, false, 1500, "start", false, 0, ""));
        LogChunk12Feedback("unbound blocked turn buttons", unboundObject, unboundStatus, turnController, auction, unboundDispatcher);

        turnController.ConfigureLiveGameplayCommandDispatcher(dispatcher, testPlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(dispatcher);
        session.Connect();
        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", "player-2", 32, false, false, false, 1500, "start", false, 0, ""));
        LogChunk12Feedback("not-local-turn blocked buttons", feedbackObject, status, turnController, auction, dispatcher);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_tile_resolution", testPlayerId, 33, true, false, false, 1490, "property_01", false, 0, ""));
        LogChunk12Feedback("turn-flag blocked buttons after roll", feedbackObject, status, turnController, auction, dispatcher);

        router.RouteRawMessage(Chunk10SnapshotResultJson("auction_bidding", testPlayerId, 34, true, true, false, 1490, "auction_test", true, 240, "player-2"));
        auction.SetBidInputTextForValidation("260");
        LogChunk12Feedback("active-auction execute-status end-turn-block", feedbackObject, status, turnController, auction, dispatcher);

        mockTransport.SetGameplayCommandTestResponsesHeld(true);
        turnController.ExecuteTile();
        LogChunk12Feedback("execute in-flight disabled reasons", feedbackObject, status, turnController, auction, dispatcher);

        string beforeExecuteDirectState = Chunk10StateSignature(hud, turnController, auction, token);
        mockTransport.SetGameplayCommandTestResponsesHeld(false);
        mockTransport.EmitCannedCommandResult(MonoJoeyTransportMessageTypes.ExecuteTile);
        string afterExecuteDirectState = Chunk10StateSignature(hud, turnController, auction, token);
        status.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 12 direct result feedback: resultType={DisplayLogValue(dispatcher.LastCommandResultType)}, waiting={dispatcher.IsWaitingForAuthoritativeSnapshot}, feedback={DisplayLogValue(turnController.CommandFeedbackText)}, status={status.LastRenderedStatus}, uiBefore={beforeExecuteDirectState}, uiAfter={afterExecuteDirectState}. Direct command success did not mutate UI before snapshot.", feedbackObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_end_turn", testPlayerId, 35, true, true, true, 1460, "auction_test", false, 0, testPlayerId));
        LogChunk12Feedback("snapshot clears waiting and updates UI", feedbackObject, status, turnController, auction, dispatcher);

        auction.SetBidInputTextForValidation("0");
        auction.SubmitLocalBidRequest();
        Debug.Log($"[AgenticTestRunner] Chunk 12 invalid bid feedback: feedback={DisplayLogValue(auction.LastBidFeedbackText)}, blocked={DisplayLogValue(auction.LastBidBlockedReason)}.", feedbackObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_end_turn", testPlayerId, 36, true, true, true, 1460, "auction_test", false, 0, testPlayerId));
        auction.SetBidInputTextForValidation("260");
        auction.SubmitLocalBidRequest();
        Debug.Log($"[AgenticTestRunner] Chunk 12 no-active-auction bid feedback: feedback={DisplayLogValue(auction.LastBidFeedbackText)}, blocked={DisplayLogValue(auction.LastBidBlockedReason)}.", feedbackObject);

        session.Disconnect();
        auction.SubmitLocalBidRequest();
        Debug.Log($"[AgenticTestRunner] Chunk 12 not-live-bound bid feedback after disconnect: feedback={DisplayLogValue(auction.LastBidFeedbackText)}, blocked={DisplayLogValue(auction.LastBidBlockedReason)}, status={session.State}.", feedbackObject);

        session.Connect();
        router.RouteRawMessage(Chunk10SnapshotResultJson("auction_bidding", testPlayerId, 37, true, true, false, 1460, "auction_test", true, 300, "player-2"));
        auction.SetBidInputTextForValidation("330");
        mockTransport.SetGameplayCommandTestResponsesHeld(true);
        auction.SubmitLocalBidRequest();
        auction.SubmitLocalBidRequest();
        Debug.Log($"[AgenticTestRunner] Chunk 12 in-flight bid feedback: inFlight={dispatcher.IsCommandInFlight}, feedback={DisplayLogValue(auction.LastBidFeedbackText)}, blocked={DisplayLogValue(auction.LastBidBlockedReason)}.", feedbackObject);

        mockTransport.EmitCannedError();
        status.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 12 backend error feedback: waiting={dispatcher.IsWaitingForAuthoritativeSnapshot}, inFlight={dispatcher.IsCommandInFlight}, turnFeedback={DisplayLogValue(turnController.CommandFeedbackText)}, bidFeedback={DisplayLogValue(auction.LastBidFeedbackText)}, status={status.LastRenderedStatus}.", feedbackObject);

        mockTransport.SetGameplayCommandTestResponsesHeld(false);
        yield return null;
    }

    private IEnumerator RunChunk16PlayableUserFlowPolishValidation(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        BoardTileController[] boardPath)
    {
        GameObject flowObject = new GameObject(
            "MonoJoeyPlayableUserFlow_Chunk16Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = flowObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = flowObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = flowObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = flowObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = flowObject.GetComponent<MonoJoeyConnectionStatusController>();

        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "session_chunk_16",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            true);
        dispatcher.Configure(session);
        status.Configure(session, router);
        turnController.ConfigureLiveGameplayCommandDispatcher(dispatcher, testPlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(dispatcher);
        session.Connect();

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", testPlayerId, 40, false, false, false, 1500, "start", false, 0, ""));
        LogChunk16FlowState("your-turn roll prompt", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "primary turn prompt is player-facing",
            ContainsAll(turnController.CurrentPlayerDisplayText, "Your turn")
                && ContainsAll(turnController.TurnStatusDisplayText, "Roll dice")
                && !ContainsProtocolDetail(turnController.CommandFeedbackText),
            $"header={DisplayLogValue(turnController.CurrentPlayerDisplayText)} status={DisplayLogValue(turnController.TurnStatusDisplayText)} feedback={DisplayLogValue(turnController.CommandFeedbackText)}",
            flowObject);

        string beforeDirectState = Chunk10StateSignature(hud, turnController, auction, token);
        mockTransport.SetGameplayCommandTestResponsesHeld(true);
        turnController.RollDice();
        LogChunk16FlowState("roll in-flight prompt", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "in-flight copy is concise",
            ContainsAll(turnController.CommandFeedbackText, "Waiting for server")
                && ContainsAll(turnController.TurnStatusDisplayText, "Roll dice in progress")
                && !ContainsProtocolDetail(turnController.CommandFeedbackText),
            $"status={DisplayLogValue(turnController.TurnStatusDisplayText)} feedback={DisplayLogValue(turnController.CommandFeedbackText)}",
            flowObject);

        mockTransport.SetGameplayCommandTestResponsesHeld(false);
        mockTransport.EmitCannedCommandResult(MonoJoeyTransportMessageTypes.RollDice);
        string afterDirectState = Chunk10StateSignature(hud, turnController, auction, token);
        LogChunk16FlowState("direct result waiting for snapshot", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "direct command result does not mutate gameplay presentation",
            string.Equals(beforeDirectState, afterDirectState, StringComparison.Ordinal)
                && ContainsAll(turnController.CommandFeedbackText, "Waiting for server update"),
            $"before={beforeDirectState} after={afterDirectState} feedback={DisplayLogValue(turnController.CommandFeedbackText)}",
            flowObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_tile_resolution", testPlayerId, 40, true, false, false, 1490, "property_01", false, 0, ""));
        LogChunk16FlowState("snapshot resolves waiting state", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "authoritative snapshot updates next action",
            !dispatcher.IsWaitingForAuthoritativeSnapshot
                && ContainsAll(turnController.TurnStatusDisplayText, "Resolve tile"),
            $"waiting={dispatcher.IsWaitingForAuthoritativeSnapshot} status={DisplayLogValue(turnController.TurnStatusDisplayText)}",
            flowObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("awaiting_roll", "player-2", 41, false, false, false, 1490, "property_01", false, 0, ""));
        LogChunk16FlowState("not-your-turn prompt", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "not-your-turn copy is readable",
            ContainsAll(turnController.CurrentPlayerDisplayText, "Waiting for player-2")
                && ContainsAll(turnController.CommandFeedbackText, "Not your turn")
                && !ContainsProtocolDetail(turnController.CommandFeedbackText),
            $"header={DisplayLogValue(turnController.CurrentPlayerDisplayText)} feedback={DisplayLogValue(turnController.CommandFeedbackText)}",
            flowObject);

        router.RouteRawMessage(Chunk10SnapshotResultJson("auction_bidding", testPlayerId, 42, true, true, false, 1490, "auction_test", true, 240, "player-2"));
        auction.SetBidInputTextForValidation("260");
        LogChunk16FlowState("active-auction prompt", flowObject, status, turnController, auction, dispatcher);
        RecordChunk16Check(
            "auction prompt is understandable without diagnostics",
            ContainsAll(turnController.TurnStatusDisplayText, "Auction in progress")
                && ContainsAll(auction.CurrentBidDisplayText, "auction_test", "$240")
                && ContainsAll(auction.HighBidderDisplayText, "player-2")
                && (ContainsAll(auction.ActivePlayerDisplayText, "Bid at least") || auction.BidButtonInteractable),
            $"turn={DisplayLogValue(turnController.TurnStatusDisplayText)} currentBid={DisplayLogValue(auction.CurrentBidDisplayText)} highBidder={DisplayLogValue(auction.HighBidderDisplayText)} prompt={DisplayLogValue(auction.ActivePlayerDisplayText)}",
            flowObject);

        auction.SetBidInputTextForValidation("0");
        auction.SubmitLocalBidRequest();
        RecordChunk16Check(
            "bid-disabled reason is player-facing",
            ContainsAll(auction.LastBidFeedbackText, "Enter a valid bid")
                && !ContainsProtocolDetail(auction.LastBidFeedbackText),
            $"bidFeedback={DisplayLogValue(auction.LastBidFeedbackText)}",
            flowObject);

        status.Refresh();
        RecordChunk16Check(
            "secondary diagnostics remain available",
            ContainsProtocolDetail(turnController.DebugLogText) || ContainsProtocolDetail(status.LastRenderedStatus),
            $"debugLog={DisplayLogValue(turnController.DebugLogText)} status={DisplayLogValue(status.LastRenderedStatus)}",
            flowObject);

        mockTransport.SetGameplayCommandTestResponsesHeld(false);
        yield return null;
    }

    private IEnumerator RunChunk11SessionJoinPanelValidation(SnapshotHydrator hydrator)
    {
        GameObject panelObject = InstantiateRequiredPrefab(sessionJoinPanelPrefab, "Assets/Prefabs/SessionJoinPanel.prefab", "SessionJoinPanel");
        if (panelObject == null)
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 11 session join panel validation skipped because SessionJoinPanel prefab was unavailable. Mock/read-only; no backend mutation.", this);
            yield break;
        }

        SessionJoinController panel = panelObject.GetComponentInChildren<SessionJoinController>();
        if (panel == null)
        {
            Debug.LogError("[AgenticTestRunner] Chunk 11 session join panel validation skipped because SessionJoinController was missing. Mock/read-only; no backend mutation.", panelObject);
            yield break;
        }

        GameObject sessionObject = new GameObject(
            "SessionJoinPanel_Chunk11MockRuntime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = sessionObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = sessionObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = sessionObject.GetComponent<MonoJoeySessionClient>();
        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "",
            "",
            hydrator,
            router,
            true,
            false,
            false);

        panel.Configure(session, router, hydrator);
        panel.SetFormValues("", "", "", MonoJoeySessionClientMode.MockValidation);
        panel.ConnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 11 empty inputs blocked connect: connectButton={panel.ConnectButtonInteractable}, error={DisplayLogValue(panel.LastValidationError)}, sentRequests={mockTransport.SentRequestTypes.Count}, state={session.State}. Expected no transport request.", panelObject);

        panel.SetFormValues("mock://session-join-validation", "session_chunk_11", testPlayerId, MonoJoeySessionClientMode.MockValidation);
        Debug.Log($"[AgenticTestRunner] Chunk 11 valid mock identity enables connect: connectButton={panel.ConnectButtonInteractable}, reconnectButton={panel.ReconnectButtonInteractable}, snapshotButton={panel.SnapshotButtonInteractable}, status={panel.LastRenderedStatus}.", panelObject);

        panel.ConnectFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 11 panel connect hydrated: state={session.State}, bound={session.IsBoundToIdentity}, reconnectHydrated={router.ReconnectResultCount}, sentTypes={string.Join(", ", mockTransport.SentRequestTypes)}, status={panel.LastRenderedStatus}. Read-only reconnect_session only.", panelObject);

        int beforeSnapshotRequests = mockTransport.SentRequestTypes.Count;
        panel.RequestSnapshotFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 11 get snapshot clicked: sentDelta={mockTransport.SentRequestTypes.Count - beforeSnapshotRequests}, snapshots={router.SnapshotResultCount}, lastRequest={DisplayLogValue(session.LastSentRequestType)}, snapshotButton={panel.SnapshotButtonInteractable}. Read-only get_snapshot only.", panelObject);

        int beforeReconnectRequests = mockTransport.SentRequestTypes.Count;
        panel.ReconnectFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 11 reconnect clicked: sentDelta={mockTransport.SentRequestTypes.Count - beforeReconnectRequests}, reconnects={session.ReconnectAttemptCount}, reconnectHydrated={router.ReconnectResultCount}, reconnectButton={panel.ReconnectButtonInteractable}. Read-only reconnect_session only.", panelObject);

        panel.DisconnectFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 11 disconnect updates state: state={session.State}, bound={session.IsBoundToIdentity}, connectButton={panel.ConnectButtonInteractable}, disconnectButton={panel.DisconnectButtonInteractable}, snapshotButton={panel.SnapshotButtonInteractable}, status={panel.LastRenderedStatus}.", panelObject);

        Debug.Log(mockTransport.GameplayMutationRequestCount == 0 && session.GameplayCommandRequestCount == 0
            ? $"[AgenticTestRunner] Chunk 11 no gameplay command sent. Sent request types={string.Join(", ", mockTransport.SentRequestTypes)}."
            : $"[AgenticTestRunner] Chunk 11 validation failed: gameplayMutations={mockTransport.GameplayMutationRequestCount}, sessionGameplayCommands={session.GameplayCommandRequestCount}.", panelObject);
    }

    private IEnumerator RunChunk14LiveSessionEntryValidation(SnapshotHydrator hydrator)
    {
        GameObject panelObject = InstantiateRequiredPrefab(sessionJoinPanelPrefab, "Assets/Prefabs/SessionJoinPanel.prefab", "SessionJoinPanelChunk14");
        if (panelObject == null)
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 14 live session entry validation skipped because SessionJoinPanel prefab was unavailable. Frontend-only/read-only; no backend mutation.", this);
            yield break;
        }

        SessionJoinController panel = panelObject.GetComponentInChildren<SessionJoinController>();
        if (panel == null)
        {
            Debug.LogError("[AgenticTestRunner] Chunk 14 live session entry validation skipped because SessionJoinController was missing. Frontend-only/read-only; no backend mutation.", panelObject);
            yield break;
        }

        GameObject sessionObject = new GameObject(
            "SessionJoinPanel_Chunk14MockRuntime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyMockTransport mockTransport = sessionObject.GetComponent<MonoJoeyMockTransport>();
        MonoJoeyBackendMessageRouter router = sessionObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = sessionObject.GetComponent<MonoJoeySessionClient>();
        session.Configure(
            MonoJoeySessionClientMode.MockValidation,
            "",
            "",
            "",
            hydrator,
            router,
            true,
            false,
            false);

        panel.Configure(session, router, hydrator);
        panel.SetFormValues("", "", "", MonoJoeySessionClientMode.MockValidation);
        panel.ConnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 14 empty URL/session/player blocks connect: error={DisplayLogValue(panel.LastValidationError)}, sentRequests={mockTransport.SentRequestTypes.Count}, state={session.State}. Expected zero outbound requests.", panelObject);

        panel.SetFormValues("http://127.0.0.1:5000/ws", "session_chunk_14", testPlayerId, MonoJoeySessionClientMode.LiveBackend);
        panel.ConnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 14 invalid live scheme blocked: error={DisplayLogValue(panel.LastValidationError)}, sentRequests={mockTransport.SentRequestTypes.Count}.", panelObject);

        panel.SetFormValues("ws://127.0.0.1:5000/not-ws", "session_chunk_14", testPlayerId, MonoJoeySessionClientMode.LiveBackend);
        panel.ConnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 14 invalid live path blocked: error={DisplayLogValue(panel.LastValidationError)}, sentRequests={mockTransport.SentRequestTypes.Count}.", panelObject);

        panel.SetFormValues("", "session_chunk_14", testPlayerId, MonoJoeySessionClientMode.LiveBackend);
        Debug.Log($"[AgenticTestRunner] Chunk 14 live mode default URL populated: contextUrl={DisplayLogValue(panel.Context == null ? "" : panel.Context.BackendUrl)}, status={panel.LastRenderedStatus}.", panelObject);

        panel.SetFormValues("mock://session-entry-validation", "session_chunk_14", testPlayerId, MonoJoeySessionClientMode.MockValidation);
        panel.ConnectFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 14 mock connect context: mode={(panel.Context == null ? MonoJoeySessionClientMode.MockValidation : panel.Context.Mode)}, url={DisplayLogValue(panel.Context == null ? "" : panel.Context.BackendUrl)}, session={DisplayLogValue(panel.Context == null ? "" : panel.Context.SessionId)}, player={DisplayLogValue(panel.Context == null ? "" : panel.Context.PlayerId)}, connected={(panel.Context != null && panel.Context.IsTransportConnected)}, bound={(panel.Context != null && panel.Context.IsBoundToIdentity)}, status={panel.LastRenderedStatus}.", panelObject);

        int beforeSnapshotRequests = mockTransport.SentRequestTypes.Count;
        panel.RequestSnapshotFromPanel();
        yield return null;
        Debug.Log($"[AgenticTestRunner] Chunk 14 mock snapshot renders players: sentDelta={mockTransport.SentRequestTypes.Count - beforeSnapshotRequests}, snapshot={DisplayLogValue(panel.Context == null ? "" : panel.Context.SnapshotSummary)}, players={DisplayLogValue(panel.LastRenderedPlayers)}.", panelObject);

        bool differingTurnHydrated = hydrator.HydrateSnapshotJson(Chunk5SnapshotJson(activeAuction: false));
        panel.Refresh();
        bool hasLocalMarker = panel.LastRenderedPlayers.Contains("[local]");
        bool hasTurnMarker = panel.LastRenderedPlayers.Contains("[turn]");
        bool localAndTurnDiffer = panel.Context != null
            && !string.IsNullOrWhiteSpace(panel.Context.SelectedPlayerId)
            && !string.IsNullOrWhiteSpace(panel.Context.CurrentTurnPlayerId)
            && !string.Equals(panel.Context.SelectedPlayerId, panel.Context.CurrentTurnPlayerId, StringComparison.Ordinal);
        Debug.Log($"[AgenticTestRunner] Chunk 14 local/current-turn markers: hydrated={differingTurnHydrated}, differ={localAndTurnDiffer}, localMarker={hasLocalMarker}, turnMarker={hasTurnMarker}, selected={DisplayLogValue(panel.Context == null ? "" : panel.Context.SelectedPlayerId)}, current={DisplayLogValue(panel.Context == null ? "" : panel.Context.CurrentTurnPlayerId)}, players={DisplayLogValue(panel.LastRenderedPlayers)}.", panelObject);

        bool onlySessionEntryRequests = ContainsOnlySessionEntryRequests(mockTransport.SentRequestTypes);
        Debug.Log(onlySessionEntryRequests && mockTransport.GameplayMutationRequestCount == 0 && session.GameplayCommandRequestCount == 0
            ? $"[AgenticTestRunner] Chunk 14 session UI sent only reconnect_session/get_snapshot: sentTypes={string.Join(", ", mockTransport.SentRequestTypes)}, gameplayMutations={mockTransport.GameplayMutationRequestCount}, gameplayCommands={session.GameplayCommandRequestCount}."
            : $"[AgenticTestRunner] Chunk 14 validation failed: sentTypes={string.Join(", ", mockTransport.SentRequestTypes)}, gameplayMutations={mockTransport.GameplayMutationRequestCount}, gameplayCommands={session.GameplayCommandRequestCount}.", panelObject);
    }

    private IEnumerator RunChunk15LiveSmokeExecutionHelperValidation(SnapshotHydrator hydrator)
    {
        GameObject mockPanelObject;
        GameObject mockSessionObject;
        LiveSmokeExecutionHelper mockHelper;
        MonoJoeyMockTransport mockTransport;
        MonoJoeySessionClient mockSession;
        MonoJoeyBackendMessageRouter mockRouter;
        MonoJoeyGameplayCommandDispatcher mockDispatcher;
        if (!CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15MockMode",
            MonoJoeySessionClientMode.MockValidation,
            false,
            out mockPanelObject,
            out mockSessionObject,
            out mockHelper,
            out mockTransport,
            out mockSession,
            out mockRouter,
            out mockDispatcher))
        {
            yield break;
        }

        bool mockStart = mockHelper.TryStartSmokeOnce();
        Debug.Log($"[AgenticTestRunner] Chunk 15 normal MockValidation helper runnable={mockStart}, result={DisplayLogValue(mockHelper.LastRunResult)}, failure={DisplayLogValue(mockHelper.LastFailureLabel)}, gameplayMutations={mockTransport.GameplayMutationRequestCount}, gameplayCommands={mockSession.GameplayCommandRequestCount}. Expected not runnable and zero gameplay commands.", mockPanelObject);

        GameObject lockedPanelObject;
        GameObject lockedSessionObject;
        LiveSmokeExecutionHelper lockedHelper;
        MonoJoeyMockTransport lockedTransport;
        MonoJoeySessionClient lockedSession;
        MonoJoeyBackendMessageRouter lockedRouter;
        MonoJoeyGameplayCommandDispatcher lockedDispatcher;
        CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15RunLock",
            MonoJoeySessionClientMode.LiveBackend,
            true,
            out lockedPanelObject,
            out lockedSessionObject,
            out lockedHelper,
            out lockedTransport,
            out lockedSession,
            out lockedRouter,
            out lockedDispatcher);
        lockedTransport.SetGameplayCommandTestResponsesHeld(true);
        bool firstStart = lockedHelper.TryStartSmokeOnce();
        yield return WaitForChunk15CommandCount(lockedDispatcher, 1, 2f);
        bool secondStart = lockedHelper.TryStartSmokeOnce();
        int commandCountAfterSecondStart = lockedDispatcher.CommandRequestCount;
        lockedHelper.CancelOrCleanupFromPanel();
        bool cooldownStart = lockedHelper.TryStartSmokeOnce();
        Debug.Log($"[AgenticTestRunner] Chunk 15 run lock/cooldown: firstStart={firstStart}, secondStart={secondStart}, cooldownStart={cooldownStart}, running={lockedHelper.IsSmokeRunning}, commandsAfterSecondStart={commandCountAfterSecondStart}, mutations={lockedTransport.GameplayMutationRequestCount}, result={DisplayLogValue(lockedHelper.LastRunResult)}, failure={DisplayLogValue(lockedHelper.LastFailureLabel)}. Expected one roll_dice max and rejected repeat/cooldown.", lockedPanelObject);

        GameObject timeoutPanelObject;
        GameObject timeoutSessionObject;
        LiveSmokeExecutionHelper timeoutHelper;
        MonoJoeyMockTransport timeoutTransport;
        MonoJoeySessionClient timeoutSession;
        MonoJoeyBackendMessageRouter timeoutRouter;
        MonoJoeyGameplayCommandDispatcher timeoutDispatcher;
        CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15Timeout",
            MonoJoeySessionClientMode.LiveBackend,
            true,
            out timeoutPanelObject,
            out timeoutSessionObject,
            out timeoutHelper,
            out timeoutTransport,
            out timeoutSession,
            out timeoutRouter,
            out timeoutDispatcher);
        timeoutTransport.SetGameplayCommandTestResponsesHeld(true);
        timeoutHelper.SetTimeoutForValidation(0.2f);
        timeoutHelper.TryStartSmokeOnce();
        yield return WaitForChunk15HelperSettled(timeoutHelper, 2f);
        Debug.Log($"[AgenticTestRunner] Chunk 15 timeout cleanup: running={timeoutHelper.IsSmokeRunning}, result={DisplayLogValue(timeoutHelper.LastRunResult)}, failure={DisplayLogValue(timeoutHelper.LastFailureLabel)}, lockReleased={!timeoutHelper.IsSmokeRunning}. Expected timeout with released run lock.", timeoutPanelObject);

        GameObject wrongTurnPanelObject;
        GameObject wrongTurnSessionObject;
        LiveSmokeExecutionHelper wrongTurnHelper;
        MonoJoeyMockTransport wrongTurnTransport;
        MonoJoeySessionClient wrongTurnSession;
        MonoJoeyBackendMessageRouter wrongTurnRouter;
        MonoJoeyGameplayCommandDispatcher wrongTurnDispatcher;
        CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15WrongTurn",
            MonoJoeySessionClientMode.LiveBackend,
            true,
            out wrongTurnPanelObject,
            out wrongTurnSessionObject,
            out wrongTurnHelper,
            out wrongTurnTransport,
            out wrongTurnSession,
            out wrongTurnRouter,
            out wrongTurnDispatcher);
        wrongTurnSession.Connect();
        wrongTurnRouter.RouteRawMessage(Chunk15SnapshotResultJson("session_chunk_8", "player-2", false, false, "awaiting_roll"));
        wrongTurnHelper.TryStartSmokeOnce();
        yield return WaitForChunk15HelperSettled(wrongTurnHelper, 2f);
        Debug.Log($"[AgenticTestRunner] Chunk 15 wrong-turn blocks before command: result={DisplayLogValue(wrongTurnHelper.LastRunResult)}, failure={DisplayLogValue(wrongTurnHelper.LastFailureLabel)}, mutations={wrongTurnTransport.GameplayMutationRequestCount}, commands={wrongTurnDispatcher.CommandRequestCount}. Expected blocked:not_local_turn and zero gameplay command mutation.", wrongTurnPanelObject);

        GameObject auctionPanelObject;
        GameObject auctionSessionObject;
        LiveSmokeExecutionHelper auctionHelper;
        MonoJoeyMockTransport auctionTransport;
        MonoJoeySessionClient auctionSession;
        MonoJoeyBackendMessageRouter auctionRouter;
        MonoJoeyGameplayCommandDispatcher auctionDispatcher;
        CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15ActiveAuction",
            MonoJoeySessionClientMode.LiveBackend,
            true,
            out auctionPanelObject,
            out auctionSessionObject,
            out auctionHelper,
            out auctionTransport,
            out auctionSession,
            out auctionRouter,
            out auctionDispatcher);
        auctionSession.Connect();
        auctionRouter.RouteRawMessage(Chunk15SnapshotResultJson("session_chunk_8", testPlayerId, false, true, "auction_bidding"));
        auctionHelper.TryStartSmokeOnce();
        yield return WaitForChunk15HelperSettled(auctionHelper, 2f);
        Debug.Log($"[AgenticTestRunner] Chunk 15 active-auction blocks before command: result={DisplayLogValue(auctionHelper.LastRunResult)}, failure={DisplayLogValue(auctionHelper.LastFailureLabel)}, mutations={auctionTransport.GameplayMutationRequestCount}, commands={auctionDispatcher.CommandRequestCount}. Expected blocked:active_auction and zero gameplay command mutation.", auctionPanelObject);

        GameObject commandPanelObject;
        GameObject commandSessionObject;
        LiveSmokeExecutionHelper commandHelper;
        MonoJoeyMockTransport commandTransport;
        MonoJoeySessionClient commandSession;
        MonoJoeyBackendMessageRouter commandRouter;
        MonoJoeyGameplayCommandDispatcher commandDispatcher;
        CreateChunk15SmokeHelperRuntime(
            hydrator,
            "SessionJoinPanel_Chunk15CommandRun",
            MonoJoeySessionClientMode.LiveBackend,
            true,
            out commandPanelObject,
            out commandSessionObject,
            out commandHelper,
            out commandTransport,
            out commandSession,
            out commandRouter,
            out commandDispatcher);
        bool commandStart = commandHelper.TryStartSmokeOnce();
        yield return WaitForChunk15HelperSettled(commandHelper, 3f);
        bool exactlyOneRoll = CountRequestType(commandTransport.SentRequestTypes, MonoJoeyTransportMessageTypes.RollDice) == 1;
        bool noOtherGameplay = CountRequestType(commandTransport.SentRequestTypes, MonoJoeyTransportMessageTypes.ResolveTile) == 0
            && CountRequestType(commandTransport.SentRequestTypes, MonoJoeyTransportMessageTypes.ExecuteTile) == 0
            && CountRequestType(commandTransport.SentRequestTypes, MonoJoeyTransportMessageTypes.EndTurn) == 0
            && CountRequestType(commandTransport.SentRequestTypes, MonoJoeyTransportMessageTypes.PlaceBid) == 0;
        bool reportComplete = !string.IsNullOrWhiteSpace(commandHelper.LastReportText)
            && commandHelper.LastReportText.Contains("result:")
            && commandHelper.LastReportText.Contains("session:")
            && commandHelper.LastReportText.Contains("player:")
            && commandHelper.LastReportText.Contains("command:")
            && commandHelper.LastReportText.Contains("direct:")
            && commandHelper.LastReportText.Contains("snapshot:");
        Debug.Log($"[AgenticTestRunner] Chunk 15 explicit mock command smoke: started={commandStart}, result={DisplayLogValue(commandHelper.LastRunResult)}, failure={DisplayLogValue(commandHelper.LastFailureLabel)}, sentTypes={string.Join(", ", commandTransport.SentRequestTypes)}, exactlyOneRoll={exactlyOneRoll}, noResolveExecuteEndBid={noOtherGameplay}, direct={DisplayLogValue(commandRouter.LastDirectCommandResultType)}, snapshots={commandRouter.SnapshotResultCount}, reportComplete={reportComplete}. Expected one roll_dice, roll_result, authoritative snapshot_result, and clipboard-ready report.", commandPanelObject);
    }

    private IEnumerator RunChunk17BoardLayoutValidation(
        SnapshotHydrator hydrator,
        PlayerTokenController token,
        TokenAnimator tokenAnimator)
    {
        GameObject tileRuntimePrefab = tilePrefab != null ? tilePrefab : LoadPrefabInEditor("Assets/Prefabs/TilePrefab.prefab");
        GameObject tokenRuntimePrefab = playerTokenPrefab != null ? playerTokenPrefab : LoadPrefabInEditor("Assets/Prefabs/PlayerToken.prefab");
        if (tileRuntimePrefab == null || tokenRuntimePrefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 17 board layout validation skipped because tile/token prefabs were unavailable. Snapshot remains authoritative; no backend mutation.");
            yield break;
        }

        GameObject boardObject = new GameObject(
            "BoardLayout_Chunk17Runtime",
            typeof(BoardLayoutManager),
            typeof(BoardCameraFramingController))
        {
            hideFlags = HideFlags.DontSave
        };
        BoardLayoutManager boardLayout = boardObject.GetComponent<BoardLayoutManager>();
        BoardCameraFramingController cameraFraming = boardObject.GetComponent<BoardCameraFramingController>();
        boardLayout.Configure(tileRuntimePrefab, tokenRuntimePrefab);
        boardLayout.SetExternalSelectedToken(token);
        hydrator.ConfigureBoardLayout(boardLayout, cameraFraming);

        bool hydrated = hydrator.HydrateSnapshotJson(Chunk17BoardLayoutSnapshotJson());
        BoardTileController[] runtimeTiles = boardLayout.RuntimeTiles;
        BoardTileController selectedTile = FindBoardTile(runtimeTiles, "property_04");
        BoardTileController currentTile = FindBoardTile(runtimeTiles, "property_02");
        BoardTileController auctionTile = FindBoardTile(runtimeTiles, "auction_test");
        bool loopExtents = boardLayout.BoardBounds.size.x > 8f && boardLayout.BoardBounds.size.z > 8f;
        bool selectedHighlight = selectedTile != null && selectedTile.CurrentHighlightKind == BoardTileController.HighlightKind.Selected;
        bool currentHighlight = currentTile != null && currentTile.CurrentHighlightKind == BoardTileController.HighlightKind.CurrentPlayer;
        bool auctionHighlight = auctionTile != null && auctionTile.CurrentHighlightKind == BoardTileController.HighlightKind.ActiveAuction;
        bool cameraFramed = cameraFraming.TargetCamera != null && cameraFraming.TargetCamera.orthographic && cameraFraming.LastOrthographicSize > 0f;

        boardLayout.TryGetToken(testPlayerId, out PlayerTokenController selectedToken);
        boardLayout.TryGetToken("player-3", out PlayerTokenController sharedTileToken);
        Vector3 selectedAnchor = boardLayout.GetTokenAnchorPosition("property_04", testPlayerId, 0.35f);
        Vector3 sharedAnchor = boardLayout.GetTokenAnchorPosition("property_04", "player-3", 0.35f);
        bool separatedSharedTileTokens = selectedToken != null
            && sharedTileToken != null
            && Vector3.Distance(selectedAnchor, sharedAnchor) > 0.05f;

        RecordChunk17Check("snapshot board generated loop layout", hydrated && runtimeTiles.Length == 12 && loopExtents, $"hydrated={hydrated}, tiles={runtimeTiles.Length}, bounds={boardLayout.BoardBounds}.", boardObject);
        RecordChunk17Check("snapshot highlight context", selectedHighlight && currentHighlight && auctionHighlight, $"selected={selectedTile?.CurrentHighlightKind}, current={currentTile?.CurrentHighlightKind}, auction={auctionTile?.CurrentHighlightKind}, activeAuctionTile={DisplayLogValue(boardLayout.ActiveAuctionTileId)}.", boardObject);
        RecordChunk17Check("shared tile token anchors separated", separatedSharedTileTokens, $"selectedAnchor={selectedAnchor}, sharedAnchor={sharedAnchor}, selectedToken={selectedToken != null}, sharedToken={sharedTileToken != null}.", boardObject);
        RecordChunk17Check("camera frames board bounds", cameraFramed, $"camera={cameraFraming.TargetCamera != null}, orthographic={(cameraFraming.TargetCamera != null && cameraFraming.TargetCamera.orthographic)}, size={cameraFraming.LastOrthographicSize}.", boardObject);

        Vector3 beforeRepeatAnchor = boardLayout.GetTokenAnchorPosition("property_04", testPlayerId, 0.35f);
        bool repeatedHydration = hydrator.HydrateSnapshotJson(Chunk17BoardLayoutSnapshotJson());
        Vector3 afterRepeatAnchor = boardLayout.GetTokenAnchorPosition("property_04", testPlayerId, 0.35f);
        RecordChunk17Check("anchors stable across repeated hydration", repeatedHydration && Vector3.Distance(beforeRepeatAnchor, afterRepeatAnchor) < 0.001f, $"before={beforeRepeatAnchor}, after={afterRepeatAnchor}, repeatedHydration={repeatedHydration}.", boardObject);

        if (tokenAnimator != null)
        {
            List<string> path = new List<string> { "property_04", "utility_01", "lockup_01" };
            tokenAnimator.AnimateAlongPath(path, boardLayout, testPlayerId, 2, TokenAnimator.MovementKind.Minimal);
            while (tokenAnimator.IsAnimating)
            {
                yield return null;
            }

            Vector3 expectedAnimationAnchor = boardLayout.GetTokenAnchorPosition("lockup_01", testPlayerId, 0.35f);
            bool animationFollowedLayoutAnchor = Vector3.Distance(tokenAnimator.FinalPosition, expectedAnimationAnchor) < 0.01f;
            RecordChunk17Check("token animation follows board anchors", animationFollowedLayoutAnchor, $"final={tokenAnimator.FinalPosition}, expected={expectedAnimationAnchor}, finalTile={DisplayLogValue(tokenAnimator.LastCompletedTileId)}.", boardObject);

            bool authoritativeRehydrate = hydrator.HydrateSnapshotJson(Chunk17BoardLayoutSnapshotJson());
            bool snapshotReturnedToken = authoritativeRehydrate
                && token != null
                && string.Equals(token.CurrentTileId, "property_04", StringComparison.Ordinal)
                && Vector3.Distance(token.transform.position, boardLayout.GetTokenAnchorPosition("property_04", testPlayerId, 0.35f)) < 0.01f;
            RecordChunk17Check("snapshot hydration remains token authority", snapshotReturnedToken, $"rehydrated={authoritativeRehydrate}, tokenTile={DisplayLogValue(token == null ? "" : token.CurrentTileId)}, tokenPosition={(token == null ? Vector3.zero : token.transform.position)}.", boardObject);
        }

        Debug.Log($"[AgenticTestRunner] Chunk 17 board layout summary: tiles={boardLayout.TileCount}, tokens={boardLayout.TokenCount}, selectedTile={DisplayLogValue(boardLayout.SelectedTileId)}, currentPlayerTile={DisplayLogValue(boardLayout.CurrentPlayerTileId)}, activeAuctionTile={DisplayLogValue(boardLayout.ActiveAuctionTileId)}, bounds={boardLayout.BoardBounds}, cameraSize={cameraFraming.LastOrthographicSize}. Placeholder visuals only; snapshot hydration remains authoritative; no backend mutation.", boardObject);
    }

    private IEnumerator RunOptionalChunk11LiveBackendSmoke(SnapshotHydrator hydrator)
    {
        if (!runChunk11LiveBackendSmoke)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(chunk11LiveWebSocketUrl)
            || string.IsNullOrWhiteSpace(chunk11LiveSessionId)
            || string.IsNullOrWhiteSpace(chunk11LivePlayerId))
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 11 live backend smoke skipped because URL/sessionId/playerId were not all provided at runtime.");
            yield break;
        }

        GameObject panelObject = InstantiateRequiredPrefab(sessionJoinPanelPrefab, "Assets/Prefabs/SessionJoinPanel.prefab", "SessionJoinPanelLiveSmoke");
        if (panelObject == null)
        {
            yield break;
        }

        SessionJoinController panel = panelObject.GetComponentInChildren<SessionJoinController>();
        if (panel == null)
        {
            Debug.LogError("[AgenticTestRunner] Chunk 11 live backend smoke skipped because SessionJoinController was missing.", panelObject);
            yield break;
        }

        GameObject liveObject = new GameObject(
            "SessionJoinPanel_Chunk11OptionalLiveRuntime",
            typeof(MonoJoeyWebSocketTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyBackendMessageRouter router = liveObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = liveObject.GetComponent<MonoJoeySessionClient>();
        session.Configure(
            MonoJoeySessionClientMode.LiveBackend,
            chunk11LiveWebSocketUrl,
            chunk11LiveSessionId,
            chunk11LivePlayerId,
            hydrator,
            router,
            true,
            false,
            false);

        panel.Configure(session, router, hydrator);
        panel.SetFormValues(chunk11LiveWebSocketUrl, chunk11LiveSessionId, chunk11LivePlayerId, MonoJoeySessionClientMode.LiveBackend);
        panel.ConnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 11 optional live smoke connect clicked: state={session.State}, lastRequest={DisplayLogValue(session.LastSentRequestType)}, status={panel.LastRenderedStatus}.");
        yield return WaitForChunk8State(session, MonoJoeyTransportConnectionState.BoundLive, 5f);
        Debug.Log($"[AgenticTestRunner] Chunk 11 optional live smoke bound: state={session.State}, bound={session.IsBoundToIdentity}, message={DisplayLogValue(router.LastMessageType)}, status={panel.LastRenderedStatus}.");

        panel.RequestSnapshotFromPanel();
        yield return WaitForChunk8Snapshot(router, 5f);
        Debug.Log($"[AgenticTestRunner] Chunk 11 optional live smoke snapshot: snapshots={router.SnapshotResultCount}, player={DisplayLogValue(hydrator.LastHydratedPlayerId)}, tile={DisplayLogValue(hydrator.LastHydratedTileId)}, status={panel.LastRenderedStatus}. No gameplay command sent.");

        panel.DisconnectFromPanel();
        Debug.Log($"[AgenticTestRunner] Chunk 11 optional live smoke disconnect: state={session.State}, status={panel.LastRenderedStatus}.");
    }

    private IEnumerator RunOptionalChunk9LiveBackendSmoke(SnapshotHydrator hydrator)
    {
        if (!runChunk9LiveBackendSmoke)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(chunk9LiveWebSocketUrl)
            || string.IsNullOrWhiteSpace(chunk9LiveSessionId)
            || string.IsNullOrWhiteSpace(chunk9LivePlayerId))
        {
            Debug.LogWarning("[AgenticTestRunner] Chunk 9 live backend smoke skipped because URL/sessionId/playerId were not all provided at runtime.");
            yield break;
        }

        GameObject liveObject = new GameObject(
            "MonoJoeyBackendTransport_Chunk8OptionalLiveRuntime",
            typeof(MonoJoeyWebSocketTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyBackendMessageRouter router = liveObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = liveObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = liveObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = liveObject.GetComponent<MonoJoeyConnectionStatusController>();
        session.Configure(
            MonoJoeySessionClientMode.LiveBackend,
            chunk9LiveWebSocketUrl,
            chunk9LiveSessionId,
            chunk9LivePlayerId,
            hydrator,
            router,
            true,
            false,
            false);
        dispatcher.Configure(session);
        status.Configure(session, router);

        session.Connect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 optional live smoke connected/reconnecting: state={session.State}, lastRequest={DisplayLogValue(session.LastSentRequestType)}.");
        yield return WaitForChunk8State(session, MonoJoeyTransportConnectionState.BoundLive, 5f);
        Debug.Log($"[AgenticTestRunner] Chunk 8 optional live smoke reconnect_result hydrated: state={session.State}, bound={session.IsBoundToIdentity}, message={DisplayLogValue(router.LastMessageType)}, status={status.LastRenderedStatus}.");

        session.RequestSnapshot();
        yield return WaitForChunk8Snapshot(router, 5f);
        Debug.Log($"[AgenticTestRunner] Chunk 8 optional live smoke snapshot_result hydrated: snapshots={router.SnapshotResultCount}, player={DisplayLogValue(hydrator.LastHydratedPlayerId)}, tile={DisplayLogValue(hydrator.LastHydratedTileId)}.");

        if (runChunk9LiveRollSmoke)
        {
            bool rollSent = dispatcher.TryRollDice();
            Debug.Log($"[AgenticTestRunner] Chunk 9 optional live roll smoke sent={rollSent}, localRequestId={DisplayLogValue(dispatcher.InFlightLocalRequestId)}. This sends roll_dice only when explicitly enabled.");
            yield return WaitForChunk9CommandSettled(dispatcher, 5f);
            Debug.Log($"[AgenticTestRunner] Chunk 9 optional live roll smoke settled: inFlight={dispatcher.IsCommandInFlight}, result={DisplayLogValue(dispatcher.LastCommandResult)}, error={DisplayLogValue(dispatcher.LastCommandError)}.");
        }

        session.Disconnect();
        Debug.Log($"[AgenticTestRunner] Chunk 8 optional live smoke disconnect: state={session.State}.");
        session.Connect();
        yield return WaitForChunk8State(session, MonoJoeyTransportConnectionState.BoundLive, 5f);
        Debug.Log($"[AgenticTestRunner] Chunk 8 optional live smoke reconnect back to BoundLive: state={session.State}, reconnects={session.ReconnectAttemptCount}.");
    }

    private IEnumerator RunOptionalChunk13LiveGameplaySmoke(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        BoardTileController[] boardPath)
    {
        if (!runChunk13LiveGameplaySmoke)
        {
            yield break;
        }

        LiveSmokeValidationRecorder recorder = new LiveSmokeValidationRecorder("Chunk 13 live gameplay smoke");
        if (string.IsNullOrWhiteSpace(chunk13LiveWebSocketUrl)
            || string.IsNullOrWhiteSpace(chunk13LiveSessionId)
            || string.IsNullOrWhiteSpace(chunk13LivePlayerId))
        {
            recorder.Fail("validation_failure", "Live smoke skipped: backendUrl/sessionId/playerId are required. No transport connection or gameplay command was sent.");
            recorder.LogSummary(this);
            yield break;
        }

        GameObject liveObject = new GameObject(
            "MonoJoeyBackendTransport_Chunk13LiveGameplayRuntime",
            typeof(MonoJoeyWebSocketTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher),
            typeof(MonoJoeyConnectionStatusController))
        {
            hideFlags = HideFlags.DontSave
        };

        MonoJoeyBackendMessageRouter router = liveObject.GetComponent<MonoJoeyBackendMessageRouter>();
        MonoJoeySessionClient session = liveObject.GetComponent<MonoJoeySessionClient>();
        MonoJoeyGameplayCommandDispatcher dispatcher = liveObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();
        MonoJoeyConnectionStatusController status = liveObject.GetComponent<MonoJoeyConnectionStatusController>();

        hydrator.Configure(hud, turnController, auction, token, tokenAnimator, boardPath, chunk13LivePlayerId);
        turnController.ConfigureLiveGameplayCommandDispatcher(dispatcher, chunk13LivePlayerId);
        auction.ConfigureLiveGameplayCommandDispatcher(dispatcher);
        session.Configure(
            MonoJoeySessionClientMode.LiveBackend,
            chunk13LiveWebSocketUrl,
            chunk13LiveSessionId,
            chunk13LivePlayerId,
            hydrator,
            router,
            true,
            false,
            false);
        dispatcher.Configure(session);
        status.Configure(session, router);

        float timeout = Mathf.Max(1f, chunk13LiveTimeoutSeconds);
        recorder.LogState(
            "transport_success",
            "connect",
            "starting",
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            0f,
            "connecting to live backend");
        session.Connect();
        yield return WaitForChunk13BoundLive(session, router, hydrator, recorder, timeout);
        if (recorder.HasFailed)
        {
            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        recorder.LogState(
            "hydration_success",
            "reconnect_session",
            "reconnect_result",
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            0f,
            "bound live after authoritative reconnect hydration");

        if (!ValidateChunk13Hydration("initial reconnect", chunk13LivePlayerId, hydrator, hud, turnController, auction, token, recorder, false))
        {
            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        yield return RunChunk13CommandStep(
            MonoJoeyTransportMessageTypes.RollDice,
            MonoJoeyTransportMessageTypes.RollResult,
            () => turnController.RollButtonInteractable,
            () => turnController.LastRollBlockedReason,
            () => dispatcher.TryRollDice(),
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            recorder,
            timeout,
            () => ValidateChunk13PostRoll(hydrator, hud, turnController, auction, token, recorder));
        if (recorder.HasFailed)
        {
            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        yield return RunChunk13CommandStep(
            MonoJoeyTransportMessageTypes.ResolveTile,
            MonoJoeyTransportMessageTypes.ResolveTileResult,
            () => turnController.ResolveButtonInteractable,
            () => turnController.LastResolveBlockedReason,
            () => dispatcher.TryResolveTile(),
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            recorder,
            timeout,
            () => ValidateChunk13TurnFlag("resolve_tile hydration", hydrator, turnController, recorder, s => s.HasResolvedTileThisTurn));
        if (recorder.HasFailed)
        {
            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        yield return RunChunk13CommandStep(
            MonoJoeyTransportMessageTypes.ExecuteTile,
            MonoJoeyTransportMessageTypes.ExecuteTileResult,
            () => turnController.ExecuteButtonInteractable,
            () => turnController.LastExecuteBlockedReason,
            () => dispatcher.TryExecuteTile(),
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            recorder,
            timeout,
            () => ValidateChunk13TurnFlag("execute_tile hydration", hydrator, turnController, recorder, s => s.HasExecutedTileThisTurn));
        if (recorder.HasFailed)
        {
            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        if (turnController.HasActiveAuctionFromHydration || auction.HasActiveAuctionSnapshot)
        {
            ValidateChunk13AuctionHydration(auction, recorder);
            recorder.LogState(
                "hydration_success",
                "auction_observed",
                "snapshot_result",
                session,
                router,
                hydrator,
                hud,
                turnController,
                auction,
                token,
                dispatcher,
                0f,
                chunk13AllowAuctionBid ? "active auction hydrated; optional bid gate enabled" : "active auction hydrated; bid gate disabled, no bid sent");

            if (recorder.HasFailed || !chunk13AllowAuctionBid)
            {
                session.Disconnect();
                recorder.LogSummary(liveObject);
                yield break;
            }

            if (chunk13AuctionBidAmount <= 0)
            {
                recorder.Fail("validation_failure", $"Auction bid gate was enabled but chunk13AuctionBidAmount={chunk13AuctionBidAmount}; no bid sent.");
                session.Disconnect();
                recorder.LogSummary(liveObject);
                yield break;
            }

            auction.SetBidInputTextForValidation(chunk13AuctionBidAmount.ToString());
            yield return RunChunk13CommandStep(
                MonoJoeyTransportMessageTypes.PlaceBid,
                MonoJoeyTransportMessageTypes.BidResult,
                () => auction.BidButtonInteractable,
                () => auction.LastBidBlockedReason,
                () =>
                {
                    auction.SubmitLocalBidRequest();
                    return string.Equals(dispatcher.LastCommandRequestType, MonoJoeyTransportMessageTypes.PlaceBid, StringComparison.Ordinal)
                        && string.IsNullOrWhiteSpace(dispatcher.LastCommandError);
                },
                session,
                router,
                hydrator,
                hud,
                turnController,
                auction,
                token,
                dispatcher,
                recorder,
                timeout,
                () => ValidateChunk13AuctionHydration(auction, recorder));

            session.Disconnect();
            recorder.LogSummary(liveObject);
            yield break;
        }

        int beforeEndTurnIndex = turnController.LastSnapshot.TurnIndex;
        string beforeEndCurrentPlayer = turnController.LastSnapshot.CurrentPlayerId;
        yield return RunChunk13CommandStep(
            MonoJoeyTransportMessageTypes.EndTurn,
            MonoJoeyTransportMessageTypes.EndTurnResult,
            () => turnController.EndTurnButtonInteractable,
            () => turnController.LastEndTurnBlockedReason,
            () => dispatcher.TryEndTurn(),
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            recorder,
            timeout,
            () => ValidateChunk13EndTurnProgression(hydrator, turnController, beforeEndTurnIndex, beforeEndCurrentPlayer, recorder));

        session.Disconnect();
        recorder.LogSummary(liveObject);
    }

    private static IEnumerator RunChunk13CommandStep(
        string commandType,
        string expectedDirectResultType,
        Func<bool> commandGate,
        Func<string> blockedReason,
        Func<bool> sendCommand,
        MonoJoeySessionClient session,
        MonoJoeyBackendMessageRouter router,
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        MonoJoeyGameplayCommandDispatcher dispatcher,
        LiveSmokeValidationRecorder recorder,
        float timeoutSeconds,
        Func<bool> validateAfterHydration)
    {
        if (recorder.HasFailed)
        {
            yield break;
        }

        if (commandGate == null || !commandGate.Invoke())
        {
            recorder.Fail("validation_failure", $"{commandType} blocked by hydrated UI/dispatcher gate: {DisplayLogValue(blockedReason == null ? "" : blockedReason.Invoke())}. No command sent.");
            recorder.LogState("validation_failure", commandType, "", session, router, hydrator, hud, turnController, auction, token, dispatcher, 0f, "command gate rejected send");
            yield break;
        }

        int initialDirectCount = router == null ? 0 : router.DirectCommandResultCount;
        int initialSnapshotCount = router == null ? 0 : router.SnapshotResultCount;
        string beforeSignature = Chunk13GameplayStateSignature(hud, turnController, auction, token);
        float started = Time.realtimeSinceStartup;

        bool sent = sendCommand != null && sendCommand.Invoke();
        recorder.LogState(
            sent ? "transport_success" : "validation_failure",
            commandType,
            "",
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            Time.realtimeSinceStartup - started,
            sent ? "state-changing command sent once" : "dispatcher rejected command before transport send");
        if (!sent)
        {
            recorder.Fail("validation_failure", $"{commandType} was not sent: {DisplayLogValue(dispatcher == null ? "" : dispatcher.LastCommandError)}.");
            yield break;
        }

        yield return WaitForChunk13DirectResult(
            expectedDirectResultType,
            initialDirectCount,
            session,
            router,
            dispatcher,
            recorder,
            timeoutSeconds);
        if (recorder.HasFailed)
        {
            recorder.LogState("validation_failure", commandType, expectedDirectResultType, session, router, hydrator, hud, turnController, auction, token, dispatcher, Time.realtimeSinceStartup - started, "direct response failed");
            yield break;
        }

        if (router != null
            && router.SnapshotResultCount <= initialSnapshotCount
            && !string.Equals(beforeSignature, Chunk13GameplayStateSignature(hud, turnController, auction, token), StringComparison.Ordinal))
        {
            recorder.Fail("validation_failure", $"{commandType} changed gameplay/UI state after direct {expectedDirectResultType} before authoritative snapshot hydration.");
            recorder.LogState("validation_failure", commandType, expectedDirectResultType, session, router, hydrator, hud, turnController, auction, token, dispatcher, Time.realtimeSinceStartup - started, "direct-response-only local mutation suspected");
            yield break;
        }

        recorder.LogState(
            "direct_command_success",
            commandType,
            expectedDirectResultType,
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            Time.realtimeSinceStartup - started,
            "direct command response observed; waiting for authoritative snapshot hydration");

        yield return WaitForChunk13SnapshotHydration(
            initialSnapshotCount,
            session,
            router,
            hydrator,
            recorder,
            timeoutSeconds);
        if (recorder.HasFailed)
        {
            recorder.LogState("validation_failure", commandType, expectedDirectResultType, session, router, hydrator, hud, turnController, auction, token, dispatcher, Time.realtimeSinceStartup - started, "authoritative hydration failed");
            yield break;
        }

        bool validated = validateAfterHydration == null || validateAfterHydration.Invoke();
        recorder.LogState(
            validated ? "hydration_success" : "validation_failure",
            commandType,
            expectedDirectResultType,
            session,
            router,
            hydrator,
            hud,
            turnController,
            auction,
            token,
            dispatcher,
            Time.realtimeSinceStartup - started,
            validated ? "authoritative hydration validated" : "authoritative hydration did not match expected post-command state");
        if (!validated && !recorder.HasFailed)
        {
            recorder.Fail("validation_failure", $"{commandType} authoritative hydration validation failed.");
        }
    }

    private static IEnumerator WaitForChunk13BoundLive(
        MonoJoeySessionClient session,
        MonoJoeyBackendMessageRouter router,
        SnapshotHydrator hydrator,
        LiveSmokeValidationRecorder recorder,
        float timeoutSeconds)
    {
        int initialReconnects = router == null ? 0 : router.ReconnectResultCount;
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (session != null && Time.realtimeSinceStartup < deadline)
        {
            if (Chunk13TransportStopped(session, router, recorder))
            {
                yield break;
            }

            if (session.State == MonoJoeyTransportConnectionState.BoundLive
                && session.IsBoundToIdentity
                && router != null
                && router.ReconnectResultCount > initialReconnects
                && hydrator != null
                && hydrator.LastHydrationSucceeded)
            {
                yield break;
            }

            yield return null;
        }

        recorder.Fail("timeout", $"Timed out waiting for reconnect_result hydration and BoundLive state after {timeoutSeconds:0.0}s.");
    }

    private static IEnumerator WaitForChunk13DirectResult(
        string expectedDirectResultType,
        int initialDirectCount,
        MonoJoeySessionClient session,
        MonoJoeyBackendMessageRouter router,
        MonoJoeyGameplayCommandDispatcher dispatcher,
        LiveSmokeValidationRecorder recorder,
        float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Chunk13TransportStopped(session, router, recorder))
            {
                yield break;
            }

            if (dispatcher != null && !string.IsNullOrWhiteSpace(dispatcher.LastBackendErrorMessage))
            {
                recorder.Fail("backend_error", dispatcher.LastCommandError);
                yield break;
            }

            if (router != null && router.DirectCommandResultCount > initialDirectCount)
            {
                if (string.Equals(router.LastDirectCommandResultType, expectedDirectResultType, StringComparison.Ordinal))
                {
                    yield break;
                }

                recorder.Fail("validation_failure", $"Expected direct result {expectedDirectResultType}, got {DisplayLogValue(router.LastDirectCommandResultType)}.");
                yield break;
            }

            yield return null;
        }

        recorder.Fail("timeout", $"Timed out waiting for direct command result {expectedDirectResultType} after {timeoutSeconds:0.0}s.");
    }

    private static IEnumerator WaitForChunk13SnapshotHydration(
        int initialSnapshotCount,
        MonoJoeySessionClient session,
        MonoJoeyBackendMessageRouter router,
        SnapshotHydrator hydrator,
        LiveSmokeValidationRecorder recorder,
        float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Chunk13TransportStopped(session, router, recorder))
            {
                yield break;
            }

            if (router != null && router.SnapshotResultCount > initialSnapshotCount)
            {
                if (hydrator != null
                    && hydrator.LastHydrationSucceeded
                    && string.Equals(hydrator.LastHydrationSourceMessageType, MonoJoeyTransportMessageTypes.SnapshotResult, StringComparison.Ordinal))
                {
                    yield break;
                }

                recorder.Fail("validation_failure", $"snapshot_result was observed but hydration failed or source was {DisplayLogValue(hydrator == null ? "" : hydrator.LastHydrationSourceMessageType)}.");
                yield break;
            }

            yield return null;
        }

        recorder.Fail("timeout", $"Timed out waiting for authoritative snapshot_result hydration after {timeoutSeconds:0.0}s.");
    }

    private static bool Chunk13TransportStopped(MonoJoeySessionClient session, MonoJoeyBackendMessageRouter router, LiveSmokeValidationRecorder recorder)
    {
        if (router != null && router.ErrorEnvelopeCount > 0)
        {
            recorder.Fail("backend_error", $"Backend error envelope code={DisplayLogValue(router.LastErrorCode)} message={DisplayLogValue(router.LastErrorMessage)}.");
            return true;
        }

        if (session == null)
        {
            recorder.Fail("validation_failure", "Session client was missing.");
            return true;
        }

        if (session.State == MonoJoeyTransportConnectionState.Error)
        {
            recorder.Fail("backend_error", $"Transport error: {DisplayLogValue(session.LastError)}.");
            return true;
        }

        if (session.State == MonoJoeyTransportConnectionState.Disconnected)
        {
            recorder.Fail("backend_error", "Transport disconnected during live smoke.");
            return true;
        }

        return false;
    }

    private static bool ValidateChunk13Hydration(
        string label,
        string expectedPlayerId,
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        LiveSmokeValidationRecorder recorder,
        bool allowCompletedGame)
    {
        if (hydrator == null || !hydrator.LastHydrationSucceeded || hydrator.LastSnapshot == null)
        {
            recorder.Fail("validation_failure", $"{label}: no successful authoritative hydration was available.");
            return false;
        }

        if (!string.Equals(hydrator.LastHydratedPlayerId, expectedPlayerId, StringComparison.Ordinal)
            || hud.LastPlayerSnapshot.PlayerId != expectedPlayerId
            || token.PlayerId != expectedPlayerId)
        {
            recorder.Fail("validation_failure", $"{label}: selected player mismatch hydrator={DisplayLogValue(hydrator.LastHydratedPlayerId)}, hud={DisplayLogValue(hud.LastPlayerSnapshot.PlayerId)}, token={DisplayLogValue(token.PlayerId)}, expected={expectedPlayerId}.");
            return false;
        }

        if (!allowCompletedGame && IsChunk13CompletedGame(hydrator.LastSnapshot))
        {
            recorder.Fail("validation_failure", $"{label}: game was already completed before the smoke flow could continue.");
            return false;
        }

        if (!turnController.HasSnapshot)
        {
            recorder.Fail("validation_failure", $"{label}: turn UI has no hydrated snapshot.");
            return false;
        }

        if (auction == null)
        {
            recorder.Fail("validation_failure", $"{label}: auction panel is missing.");
            return false;
        }

        return true;
    }

    private static bool ValidateChunk13PostRoll(
        SnapshotHydrator hydrator,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        LiveSmokeValidationRecorder recorder)
    {
        if (!ValidateChunk13Hydration("roll_dice hydration", token.PlayerId, hydrator, hud, turnController, auction, token, recorder, true))
        {
            return false;
        }

        if (!IsChunk13CompletedGame(hydrator.LastSnapshot) && !turnController.LastSnapshot.HasRolledThisTurn)
        {
            recorder.Fail("validation_failure", "roll_dice hydration did not progress hasRolledThisTurn.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(hydrator.LastHydratedTileId) || token.CurrentTileIndex < 0)
        {
            recorder.Fail("validation_failure", $"roll_dice hydration did not record tile/token data: tile={DisplayLogValue(hydrator.LastHydratedTileId)}, tokenIndex={token.CurrentTileIndex}.");
            return false;
        }

        return true;
    }

    private static bool ValidateChunk13TurnFlag(
        string label,
        SnapshotHydrator hydrator,
        TurnController turnController,
        LiveSmokeValidationRecorder recorder,
        Func<HUDController.TurnHudSnapshot, bool> flagPredicate)
    {
        if (hydrator == null || hydrator.LastSnapshot == null || !hydrator.LastHydrationSucceeded)
        {
            recorder.Fail("validation_failure", $"{label}: no successful snapshot hydration was available.");
            return false;
        }

        if (IsChunk13CompletedGame(hydrator.LastSnapshot))
        {
            return true;
        }

        if (flagPredicate == null || !flagPredicate.Invoke(turnController.LastSnapshot))
        {
            recorder.Fail("validation_failure", $"{label}: expected turn flag was not set by authoritative snapshot.");
            return false;
        }

        return true;
    }

    private static bool ValidateChunk13AuctionHydration(AuctionPanelController auction, LiveSmokeValidationRecorder recorder)
    {
        if (auction == null || !auction.HasActiveAuctionSnapshot)
        {
            recorder.Fail("validation_failure", "Expected active auction hydration but auction UI has no active auction snapshot.");
            return false;
        }

        if (auction.CurrentHighBid < 0)
        {
            recorder.Fail("validation_failure", $"Auction hydration produced invalid high bid {auction.CurrentHighBid}.");
            return false;
        }

        return true;
    }

    private static bool ValidateChunk13EndTurnProgression(
        SnapshotHydrator hydrator,
        TurnController turnController,
        int beforeTurnIndex,
        string beforeCurrentPlayerId,
        LiveSmokeValidationRecorder recorder)
    {
        if (hydrator == null || hydrator.LastSnapshot == null || !hydrator.LastHydrationSucceeded)
        {
            recorder.Fail("validation_failure", "end_turn hydration failed.");
            return false;
        }

        if (IsChunk13CompletedGame(hydrator.LastSnapshot))
        {
            return true;
        }

        bool turnProgressed = turnController.LastSnapshot.TurnIndex > beforeTurnIndex
            || !string.Equals(turnController.LastSnapshot.CurrentPlayerId, beforeCurrentPlayerId, StringComparison.Ordinal);
        if (!turnProgressed)
        {
            recorder.Fail("validation_failure", $"end_turn hydration did not progress turn: before={beforeTurnIndex}/{DisplayLogValue(beforeCurrentPlayerId)}, after={turnController.LastSnapshot.TurnIndex}/{DisplayLogValue(turnController.LastSnapshot.CurrentPlayerId)}.");
            return false;
        }

        return true;
    }

    private static bool IsChunk13CompletedGame(MonoJoeySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return false;
        }

        return string.Equals(snapshot.gameStatus, "completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(snapshot.status, "completed", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(snapshot.winnerPlayerId)
            || !string.IsNullOrWhiteSpace(snapshot.endedAtUtc);
    }

    private static string Chunk13GameplayStateSignature(
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token)
    {
        HUDController.PlayerHudSnapshot player = hud.LastPlayerSnapshot;
        HUDController.TurnHudSnapshot turn = turnController.LastSnapshot;
        return $"player={DisplayLogValue(player.PlayerId)},money={player.Money},tile={DisplayLogValue(player.CurrentTileId)},turn={turn.TurnIndex}/{DisplayLogValue(turn.CurrentPlayerId)}/{turn.HasRolledThisTurn}/{turn.HasResolvedTileThisTurn}/{turn.HasExecutedTileThisTurn},auction={DisplayLogValue(auction.AuctionId)}/{auction.CurrentHighBid}/{DisplayLogValue(auction.HighBidderPlayerId)},token={DisplayLogValue(token.PlayerId)}/{token.CurrentTileIndex}";
    }

    private sealed class LiveSmokeValidationRecorder
    {
        private readonly string name;
        private readonly List<string> entries = new List<string>();

        public LiveSmokeValidationRecorder(string recorderName)
        {
            name = recorderName;
        }

        public bool HasFailed { get; private set; }
        public string FailureLabel { get; private set; } = "";
        public string FailureMessage { get; private set; } = "";

        public void Fail(string label, string message)
        {
            if (!HasFailed)
            {
                HasFailed = true;
                FailureLabel = label ?? "validation_failure";
                FailureMessage = message ?? "";
            }

            Log($"{label}: {message}", null, true);
        }

        public void LogState(
            string label,
            string sentCommand,
            string directResponseType,
            MonoJoeySessionClient session,
            MonoJoeyBackendMessageRouter router,
            SnapshotHydrator hydrator,
            HUDController hud,
            TurnController turnController,
            AuctionPanelController auction,
            PlayerTokenController token,
            MonoJoeyGameplayCommandDispatcher dispatcher,
            float elapsedSeconds,
            string validationResult)
        {
            HUDController.PlayerHudSnapshot player = hud.LastPlayerSnapshot;
            HUDController.TurnHudSnapshot turn = turnController.LastSnapshot;
            string message =
                $"{label}: sent={DisplayLogValue(sentCommand)}, direct={DisplayLogValue(directResponseType)}, directError={DisplayLogValue(dispatcher == null ? "" : dispatcher.LastCommandError)}, " +
                $"hydration={DisplayLogValue(hydrator == null ? "" : hydrator.LastHydrationSourceMessageType)}@{DisplayLogValue(hydrator == null || hydrator.LastHydrationUtc == DateTime.MinValue ? "" : hydrator.LastHydrationUtc.ToString("O"))}, snapshotVersion={(hydrator == null ? 0 : hydrator.LastHydratedSnapshotVersion)}, " +
                $"player={DisplayLogValue(player.PlayerId)} money={player.Money} tile={DisplayLogValue(player.CurrentTileId)}, turn={turn.TurnIndex}/{DisplayLogValue(turn.CurrentPlayerId)} flags={turn.HasRolledThisTurn}/{turn.HasResolvedTileThisTurn}/{turn.HasExecutedTileThisTurn}, " +
                $"auction={DisplayLogValue(auction == null ? "" : auction.AuctionId)} highBid={(auction == null ? 0 : auction.CurrentHighBid)} highBidder={DisplayLogValue(auction == null ? "" : auction.HighBidderPlayerId)}, " +
                $"token={DisplayLogValue(token == null ? "" : token.PlayerId)} index={(token == null ? -1 : token.CurrentTileIndex)} position={(token == null ? Vector3.zero : token.transform.position)}, " +
                $"sequences=last:{(router == null ? 0 : router.LastSequence)} advisory:{(router == null ? 0 : router.AdvisoryLastEventSequence)} snapshots:{(router == null ? 0 : router.SnapshotResultCount)} broadcasts:{(router == null ? 0 : router.SequencedBroadcastCount)} direct:{(router == null ? 0 : router.DirectCommandResultCount)}, " +
                $"state={(session == null ? MonoJoeyTransportConnectionState.Error : session.State)}, elapsed={elapsedSeconds:0.000}s, validation={DisplayLogValue(validationResult)}";
            Log(message, null, false);
        }

        public void LogSummary(UnityEngine.Object context)
        {
            string result = HasFailed
                ? $"failed label={DisplayLogValue(FailureLabel)} message={DisplayLogValue(FailureMessage)}"
                : "passed bounded live gameplay smoke";
            Log($"summary: {result}; steps={entries.Count}", context, HasFailed);
        }

        private void Log(string message, UnityEngine.Object context, bool warning)
        {
            string line = $"[AgenticTestRunner] {DateTime.UtcNow:O} {name} {message}. Backend authoritative; no local gameplay state mutation.";
            entries.Add(line);
            if (warning)
            {
                Debug.LogWarning(line, context);
            }
            else
            {
                Debug.Log(line, context);
            }
        }
    }

    private static IEnumerator WaitForChunk8State(MonoJoeySessionClient session, MonoJoeyTransportConnectionState state, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (session != null && session.State != state && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForChunk8Snapshot(MonoJoeyBackendMessageRouter router, float timeoutSeconds)
    {
        int initialCount = router == null ? 0 : router.SnapshotResultCount;
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (router != null && router.SnapshotResultCount <= initialCount && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForChunk9CommandSettled(MonoJoeyGameplayCommandDispatcher dispatcher, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (dispatcher != null && dispatcher.IsCommandInFlight && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static string LastSentMessageForType(MonoJoeyMockTransport transport, string requestType)
    {
        if (transport == null || transport.SentMessages == null || transport.SentRequestTypes == null)
        {
            return "--";
        }

        for (int i = transport.SentRequestTypes.Count - 1; i >= 0; i--)
        {
            if (string.Equals(transport.SentRequestTypes[i], requestType, StringComparison.Ordinal))
            {
                return i < transport.SentMessages.Count ? transport.SentMessages[i] : "--";
            }
        }

        return "--";
    }

    private static bool ContainsOnlySessionEntryRequests(IReadOnlyList<string> requestTypes)
    {
        if (requestTypes == null)
        {
            return true;
        }

        for (int i = 0; i < requestTypes.Count; i++)
        {
            string requestType = requestTypes[i] ?? "";
            if (!string.Equals(requestType, "reconnect_session", StringComparison.Ordinal)
                && !string.Equals(requestType, "get_snapshot", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool CreateChunk15SmokeHelperRuntime(
        SnapshotHydrator hydrator,
        string objectName,
        MonoJoeySessionClientMode mode,
        bool enableCommandTestMode,
        out GameObject panelObject,
        out GameObject sessionObject,
        out LiveSmokeExecutionHelper helper,
        out MonoJoeyMockTransport mockTransport,
        out MonoJoeySessionClient session,
        out MonoJoeyBackendMessageRouter router,
        out MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        panelObject = InstantiateRequiredPrefab(sessionJoinPanelPrefab, "Assets/Prefabs/SessionJoinPanel.prefab", objectName);
        sessionObject = null;
        helper = null;
        mockTransport = null;
        session = null;
        router = null;
        dispatcher = null;

        if (panelObject == null)
        {
            Debug.LogWarning($"[AgenticTestRunner] Chunk 15 validation skipped because SessionJoinPanel prefab was unavailable for {objectName}.", this);
            return false;
        }

        SessionJoinController panel = panelObject.GetComponentInChildren<SessionJoinController>();
        helper = panelObject.GetComponentInChildren<LiveSmokeExecutionHelper>();
        if (panel == null || helper == null)
        {
            Debug.LogError($"[AgenticTestRunner] Chunk 15 validation skipped because panel components were missing: SessionJoinController={panel != null}, LiveSmokeExecutionHelper={helper != null}.", panelObject);
            return false;
        }

        sessionObject = new GameObject(
            objectName + "_Runtime",
            typeof(MonoJoeyMockTransport),
            typeof(MonoJoeyBackendMessageRouter),
            typeof(MonoJoeySessionClient),
            typeof(MonoJoeyGameplayCommandDispatcher))
        {
            hideFlags = HideFlags.DontSave
        };

        mockTransport = sessionObject.GetComponent<MonoJoeyMockTransport>();
        router = sessionObject.GetComponent<MonoJoeyBackendMessageRouter>();
        session = sessionObject.GetComponent<MonoJoeySessionClient>();
        dispatcher = sessionObject.GetComponent<MonoJoeyGameplayCommandDispatcher>();

        session.Configure(
            mode,
            "ws://127.0.0.1:5000/ws",
            "session_chunk_8",
            testPlayerId,
            hydrator,
            router,
            true,
            false,
            enableCommandTestMode);
        session.BindTransportForValidation(mockTransport);
        dispatcher.Configure(session);
        panel.Configure(session, router, hydrator);
        panel.SetFormValues("ws://127.0.0.1:5000/ws", "session_chunk_8", testPlayerId, mode);
        helper.Configure(panel, session, router, hydrator, panel.Context, dispatcher);
        helper.SetTimeoutForValidation(1f);
        return true;
    }

    private static IEnumerator WaitForChunk15HelperSettled(LiveSmokeExecutionHelper helper, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (helper != null && helper.IsSmokeRunning && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForChunk15CommandCount(MonoJoeyGameplayCommandDispatcher dispatcher, int expectedCount, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (dispatcher != null && dispatcher.CommandRequestCount < expectedCount && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static int CountRequestType(IReadOnlyList<string> requestTypes, string requestType)
    {
        if (requestTypes == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < requestTypes.Count; i++)
        {
            if (string.Equals(requestTypes[i], requestType, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static void SubscribeToChunk6HydratorHooks(SnapshotHydrator hydrator, UnityEngine.Object logContext)
    {
        if (hydrator == null)
        {
            return;
        }

        hydrator.HydrationStarted += LogChunk6HydratorHook;
        hydrator.HudUpdated += LogChunk6HydratorHook;
        hydrator.TurnUpdated += LogChunk6HydratorHook;
        hydrator.AuctionUpdated += LogChunk6HydratorHook;
        hydrator.BoardTileUpdated += LogChunk6HydratorHook;
        hydrator.TokenUpdated += LogChunk6HydratorHook;
        hydrator.HydrationCompleted += LogChunk6HydratorHook;
        hydrator.HydrationFailed += LogChunk6HydratorHook;

        Debug.Log($"[AgenticTestRunner] {UtcNowStamp()} Chunk 6 live session hook subscribed. Read-only mock hooks; no backend mutation.", logContext);
    }

    private static void LogChunk6HydratorHook(SnapshotHydrator.HydrationHookEvent hookEvent)
    {
        Debug.Log($"[AgenticTestRunner] {hookEvent.UtcTimestamp:O} Chunk 6 hydrator hook: hook={hookEvent.HookName}, success={hookEvent.Succeeded}, session={DisplayLogValue(hookEvent.SessionId)}, player={DisplayLogValue(hookEvent.PlayerId)}, tile={DisplayLogValue(hookEvent.TileId)}, phase={DisplayLogValue(hookEvent.Phase)}, turn={hookEvent.TurnIndex}, message={hookEvent.Message}. Read-only mock hook; no backend mutation.");
    }

    private static string UtcNowStamp()
    {
        return DateTime.UtcNow.ToString("O");
    }

    private static string DisplayLogValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private static BoardTileController FindBoardTile(IReadOnlyList<BoardTileController> boardPath, string tileId)
    {
        if (boardPath == null)
        {
            return null;
        }

        for (int i = 0; i < boardPath.Count; i++)
        {
            BoardTileController tile = boardPath[i];
            if (tile != null && tile.TileId == tileId)
            {
                return tile;
            }
        }

        return null;
    }

    private static void LogChunk10UiState(
        string label,
        UnityEngine.Object context,
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator tokenAnimator,
        SnapshotHydrator hydrator,
        MonoJoeyGameplayCommandDispatcher dispatcher,
        MonoJoeyMockTransport mockTransport,
        IReadOnlyList<BoardTileController> boardPath)
    {
        BoardTileController currentTile = FindBoardTile(boardPath, hud == null ? "" : hud.LastPlayerSnapshot.CurrentTileId);
        Debug.Log($"[AgenticTestRunner] Chunk 10 {label}: hydrationSource={DisplayLogValue(hydrator.LastHydrationSourceMessageType)}, hudMoney={hud.LastPlayerSnapshot.Money}, hudTile={DisplayLogValue(hud.LastPlayerSnapshot.CurrentTileId)}, turn={turnController.LastSnapshot.TurnIndex}/{DisplayLogValue(turnController.LastSnapshot.Phase)}, flags=rolled:{turnController.LastSnapshot.HasRolledThisTurn},resolved:{turnController.LastSnapshot.HasResolvedTileThisTurn},executed:{turnController.LastSnapshot.HasExecutedTileThisTurn}, activeAuction={turnController.HasActiveAuctionFromHydration}, auctionId={DisplayLogValue(auction.AuctionId)}, highBid={auction.CurrentHighBid}, bidButton={auction.BidButtonInteractable}, buttons=roll:{turnController.RollButtonInteractable},resolve:{turnController.ResolveButtonInteractable},execute:{turnController.ExecuteButtonInteractable},end:{turnController.EndTurnButtonInteractable}, blocked=roll:{DisplayLogValue(turnController.LastRollBlockedReason)},resolve:{DisplayLogValue(turnController.LastResolveBlockedReason)},execute:{DisplayLogValue(turnController.LastExecuteBlockedReason)},end:{DisplayLogValue(turnController.LastEndTurnBlockedReason)},bid:{DisplayLogValue(auction.LastBidBlockedReason)}, tokenPlayer={DisplayLogValue(token.PlayerId)}, tokenTileIndex={token.CurrentTileIndex}, tokenPosition={token.transform.position}, tokenAnimatorFinal={DisplayLogValue(tokenAnimator.LastCompletedTileId)}, boardTileOwner={DisplayLogValue(currentTile == null ? "" : currentTile.OwnerPlayerId)}, boardTileHighlighted={(currentTile != null && currentTile.IsHighlighted)}, dispatcherInFlight={dispatcher.IsCommandInFlight}, dispatcherInFlightLog={DisplayLogValue(dispatcher.LastInFlightStateLog)}, requestCount={dispatcher.CommandRequestCount}, mockMutationRequests={mockTransport.GameplayMutationRequestCount}. Backend authoritative; no local gameplay state mutation.", context);
    }

    private static void LogChunk12Feedback(
        string label,
        UnityEngine.Object context,
        MonoJoeyConnectionStatusController status,
        TurnController turnController,
        AuctionPanelController auction,
        MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        status?.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 12 {label}: buttons=roll:{turnController.RollButtonInteractable},resolve:{turnController.ResolveButtonInteractable},execute:{turnController.ExecuteButtonInteractable},end:{turnController.EndTurnButtonInteractable}, blocked=roll:{DisplayLogValue(turnController.LastRollBlockedReason)},resolve:{DisplayLogValue(turnController.LastResolveBlockedReason)},execute:{DisplayLogValue(turnController.LastExecuteBlockedReason)},end:{DisplayLogValue(turnController.LastEndTurnBlockedReason)}, turnFeedback={DisplayLogValue(turnController.CommandFeedbackText)}, bidButton={auction.BidButtonInteractable}, bidBlocked={DisplayLogValue(auction.LastBidBlockedReason)}, bidFeedback={DisplayLogValue(auction.LastBidFeedbackText)}, inFlight={dispatcher.IsCommandInFlight}, waiting={dispatcher.IsWaitingForAuthoritativeSnapshot}, result={DisplayLogValue(dispatcher.LastCommandResultType)}, backendError={DisplayLogValue(dispatcher.LastCommandError)}, status={DisplayLogValue(status == null ? "" : status.LastRenderedStatus)}. Backend authoritative; no local gameplay state mutation.", context);
    }

    private static void LogChunk16FlowState(
        string label,
        UnityEngine.Object context,
        MonoJoeyConnectionStatusController status,
        TurnController turnController,
        AuctionPanelController auction,
        MonoJoeyGameplayCommandDispatcher dispatcher)
    {
        status?.Refresh();
        Debug.Log($"[AgenticTestRunner] Chunk 16 {label}: primaryHeader={DisplayLogValue(turnController.CurrentPlayerDisplayText)}, primaryStatus={DisplayLogValue(turnController.TurnStatusDisplayText)}, primaryFeedback={DisplayLogValue(turnController.CommandFeedbackText)}, auctionCurrent={DisplayLogValue(auction.CurrentBidDisplayText)}, auctionPrompt={DisplayLogValue(auction.ActivePlayerDisplayText)}, auctionHighBidder={DisplayLogValue(auction.HighBidderDisplayText)}, bidFeedback={DisplayLogValue(auction.LastBidFeedbackText)}, diagnosticsStatus={DisplayLogValue(status == null ? "" : status.LastRenderedStatus)}, debugAvailable={!string.IsNullOrWhiteSpace(turnController.DebugLogText)}, inFlight={dispatcher.IsCommandInFlight}, waiting={dispatcher.IsWaitingForAuthoritativeSnapshot}. Backend authoritative; no local gameplay state mutation.", context);
    }

    private static void RecordChunk16Check(string label, bool passed, string details, UnityEngine.Object context)
    {
        string message = $"[AgenticTestRunner] Chunk 16 {(passed ? "PASS" : "FAIL")} {label}: {details}.";
        if (passed)
        {
            Debug.Log(message, context);
            return;
        }

        Debug.LogError(message, context);
    }

    private static void RecordChunk17Check(string label, bool passed, string details, UnityEngine.Object context)
    {
        string message = $"[AgenticTestRunner] Chunk 17 {(passed ? "PASS" : "FAIL")} {label}: {details}";
        if (passed)
        {
            Debug.Log(message, context);
            return;
        }

        Debug.LogError(message, context);
    }

    private static bool ContainsAll(string value, params string[] expectedFragments)
    {
        if (value == null)
        {
            return false;
        }

        for (int i = 0; i < expectedFragments.Length; i++)
        {
            string fragment = expectedFragments[i];
            if (!string.IsNullOrWhiteSpace(fragment)
                && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsProtocolDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.IndexOf("roll_dice", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("resolve_tile", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("execute_tile", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("end_turn", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("place_bid", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("localRequestId", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("requestId", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("sequence", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("snapshot_result", StringComparison.OrdinalIgnoreCase) >= 0
            || value.IndexOf("reconnect_result", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Chunk10StateSignature(
        HUDController hud,
        TurnController turnController,
        AuctionPanelController auction,
        PlayerTokenController token)
    {
        return $"money={hud.LastPlayerSnapshot.Money},tile={DisplayLogValue(hud.LastPlayerSnapshot.CurrentTileId)},phase={DisplayLogValue(turnController.LastSnapshot.Phase)},flags={turnController.LastSnapshot.HasRolledThisTurn}/{turnController.LastSnapshot.HasResolvedTileThisTurn}/{turnController.LastSnapshot.HasExecutedTileThisTurn},auction={DisplayLogValue(auction.AuctionId)}/{auction.CurrentHighBid},token={token.CurrentTileIndex}";
    }

    private static string Chunk10SnapshotResultJson(
        string phase,
        string currentPlayerId,
        int turnIndex,
        bool rolled,
        bool resolved,
        bool executed,
        int localMoney,
        string localTileId,
        bool activeAuction,
        int auctionHighBid,
        string auctionHighBidderId)
    {
        string auctionJson = activeAuction
            ? $@",
    ""activeAuction"": {{
      ""propertyTileId"": ""auction_test"",
      ""triggeringPlayerId"": ""player-agentic"",
      ""status"": ""active"",
      ""startingBid"": 100,
      ""minimumBidIncrement"": 10,
      ""initialPreBidSeconds"": 5,
      ""bidResetSeconds"": 10,
      ""highestBid"": {auctionHighBid},
      ""highestBidderId"": ""{auctionHighBidderId}"",
      ""countdownDurationSeconds"": 11,
      ""timerEndsAtUtc"": ""2026-05-11T00:10:11Z"",
      ""bids"": [
        {{ ""bidderPlayerId"": ""player-agentic"", ""amount"": 220, ""placedAtUtc"": ""2026-05-11T00:10:01Z"" }},
        {{ ""bidderPlayerId"": ""player-2"", ""amount"": {auctionHighBid}, ""placedAtUtc"": ""2026-05-11T00:10:02Z"" }}
      ]
    }}"
            : @",
    ""activeAuction"": null";

        return $@"{{
  ""type"": ""snapshot_result"",
  ""payload"": {{
    ""snapshotVersion"": 10,
    ""sessionId"": ""session_chunk_10"",
    ""status"": ""in_game"",
    ""gameStatus"": ""in_progress"",
    ""serverNowUtc"": ""2026-05-11T00:10:00Z"",
    ""matchId"": ""session_chunk_10"",
    ""phase"": ""{phase}"",
    ""turn"": {{ ""currentPlayerId"": ""{currentPlayerId}"", ""turnIndex"": {turnIndex}, ""hasRolledThisTurn"": {JsonBool(rolled)}, ""hasResolvedTileThisTurn"": {JsonBool(resolved)}, ""hasExecutedTileThisTurn"": {JsonBool(executed)} }},
    ""players"": [
      {{ ""playerId"": ""player-agentic"", ""username"": ""Agentic Player"", ""tokenId"": ""token_agentic"", ""colorId"": ""gold"", ""money"": {localMoney}, ""currentTileId"": ""{localTileId}"", ""ownedPropertyIds"": [], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": {{ ""totalBorrowed"": 120, ""currentInterestRatePercent"": 10, ""nextTurnInterestDue"": 12, ""loanTier"": 1 }}, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }},
      {{ ""playerId"": ""player-2"", ""username"": ""Blue Player"", ""tokenId"": ""token_blue"", ""colorId"": ""blue"", ""money"": 1580, ""currentTileId"": ""property_01"", ""ownedPropertyIds"": [""property_01""], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": {{ ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 }}, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }}
    ],
    ""board"": {{
      ""boardId"": ""chunk_10_board"",
      ""version"": 10,
      ""displayName"": ""Chunk 10 Board"",
      ""tiles"": [
        {{ ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null }},
        {{ ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" }},
        {{ ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" }},
        {{ ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": {OwnerJson(auctionHighBidderId)} }}
      ]
    }}{auctionJson},
    ""movement"": {{ ""playerId"": ""player-agentic"", ""fromTileId"": ""start"", ""toTileId"": ""{localTileId}"", ""pathTileIds"": [""{localTileId}""], ""stepCount"": 1, ""movementKind"": ""snap"", ""passedStart"": false }},
    ""moneyDeltas"": [],
    ""propertyOwnershipChanges"": [],
    ""playerEliminations"": []
  }}
}}";
    }

    private static string Chunk15SnapshotResultJson(
        string sessionId,
        string currentPlayerId,
        bool rolled,
        bool activeAuction,
        string phase)
    {
        string auctionJson = activeAuction
            ? @",
    ""activeAuction"": {
      ""propertyTileId"": ""auction_test"",
      ""triggeringPlayerId"": ""player-agentic"",
      ""status"": ""active"",
      ""startingBid"": 100,
      ""minimumBidIncrement"": 10,
      ""initialPreBidSeconds"": 5,
      ""bidResetSeconds"": 10,
      ""highestBid"": 300,
      ""highestBidderId"": ""player-2"",
      ""countdownDurationSeconds"": 10,
      ""timerEndsAtUtc"": ""2026-05-11T00:15:10Z"",
      ""bids"": [
        { ""bidderPlayerId"": ""player-2"", ""amount"": 300, ""placedAtUtc"": ""2026-05-11T00:15:01Z"" }
      ]
    }"
            : @",
    ""activeAuction"": null";

        return $@"{{
  ""type"": ""snapshot_result"",
  ""payload"": {{
    ""snapshotVersion"": 15,
    ""sessionId"": ""{sessionId}"",
    ""status"": ""in_game"",
    ""gameStatus"": ""in_progress"",
    ""serverNowUtc"": ""2026-05-11T00:15:00Z"",
    ""matchId"": ""{sessionId}"",
    ""phase"": ""{phase}"",
    ""turn"": {{ ""currentPlayerId"": ""{currentPlayerId}"", ""turnIndex"": 15, ""hasRolledThisTurn"": {JsonBool(rolled)}, ""hasResolvedTileThisTurn"": false, ""hasExecutedTileThisTurn"": false }},
    ""players"": [
      {{ ""playerId"": ""player-agentic"", ""username"": ""Agentic Player"", ""tokenId"": ""token_agentic"", ""colorId"": ""gold"", ""money"": 1260, ""currentTileId"": ""start"", ""ownedPropertyIds"": [], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": {{ ""totalBorrowed"": 240, ""currentInterestRatePercent"": 25, ""nextTurnInterestDue"": 60, ""loanTier"": 2 }}, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }},
      {{ ""playerId"": ""player-2"", ""username"": ""Blue Player"", ""tokenId"": ""token_blue"", ""colorId"": ""blue"", ""money"": 1580, ""currentTileId"": ""property_01"", ""ownedPropertyIds"": [], ""heldCardIds"": [], ""statusEffects"": [], ""loan"": {{ ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 }}, ""isBankrupt"": false, ""isEliminated"": false, ""isLockedUp"": false }}
    ],
    ""board"": {{
      ""boardId"": ""chunk_15_board"",
      ""version"": 15,
      ""displayName"": ""Chunk 15 Board"",
      ""tiles"": [
        {{ ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null }},
        {{ ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" }},
        {{ ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" }},
        {{ ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": null }}
      ]
    }}{auctionJson},
    ""movement"": null,
    ""moneyDeltas"": [],
    ""propertyOwnershipChanges"": [],
    ""playerEliminations"": []
  }}
}}";
    }

    private static string Chunk17BoardLayoutSnapshotJson()
    {
        return @"{
  ""snapshotVersion"": 17,
  ""sessionId"": ""session_chunk_17"",
  ""status"": ""in_game"",
  ""gameStatus"": ""in_progress"",
  ""serverNowUtc"": ""2026-05-12T00:17:00Z"",
  ""matchId"": ""session_chunk_17"",
  ""phase"": ""auction_bidding"",
  ""turn"": {
    ""currentPlayerId"": ""player-2"",
    ""turnIndex"": 17,
    ""hasRolledThisTurn"": true,
    ""hasResolvedTileThisTurn"": true,
    ""hasExecutedTileThisTurn"": false
  },
  ""players"": [
    {
      ""playerId"": ""player-agentic"",
      ""username"": ""Agentic Player"",
      ""tokenId"": ""token_agentic"",
      ""colorId"": ""gold"",
      ""money"": 1400,
      ""currentTileId"": ""property_04"",
      ""ownedPropertyIds"": [""property_01""],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": { ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 },
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    },
    {
      ""playerId"": ""player-2"",
      ""username"": ""Blue Player"",
      ""tokenId"": ""token_blue"",
      ""colorId"": ""blue"",
      ""money"": 1500,
      ""currentTileId"": ""property_02"",
      ""ownedPropertyIds"": [""property_02""],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": { ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 },
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    },
    {
      ""playerId"": ""player-3"",
      ""username"": ""Green Player"",
      ""tokenId"": ""token_green"",
      ""colorId"": ""green"",
      ""money"": 1500,
      ""currentTileId"": ""property_04"",
      ""ownedPropertyIds"": [],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": { ""totalBorrowed"": 0, ""currentInterestRatePercent"": 0, ""nextTurnInterestDue"": 0, ""loanTier"": 0 },
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    }
  ],
  ""board"": {
    ""boardId"": ""chunk_17_loop_board"",
    ""version"": 17,
    ""displayName"": ""Chunk 17 Loop Board"",
    ""tiles"": [
      { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null },
      { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" },
      { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" },
      { ""tileId"": ""chance_01"", ""index"": 3, ""displayName"": ""Chance 01"", ""tileType"": ""chance_deck"", ""ownerPlayerId"": null },
      { ""tileId"": ""property_03"", ""index"": 4, ""displayName"": ""Property 03"", ""tileType"": ""property"", ""ownerPlayerId"": null },
      { ""tileId"": ""property_04"", ""index"": 5, ""displayName"": ""Property 04"", ""tileType"": ""property"", ""ownerPlayerId"": null },
      { ""tileId"": ""utility_01"", ""index"": 6, ""displayName"": ""Utility 01"", ""tileType"": ""utility"", ""ownerPlayerId"": null },
      { ""tileId"": ""lockup_01"", ""index"": 7, ""displayName"": ""Lockup"", ""tileType"": ""lockup"", ""ownerPlayerId"": null },
      { ""tileId"": ""auction_test"", ""index"": 8, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": null },
      { ""tileId"": ""transport_01"", ""index"": 9, ""displayName"": ""Transport 01"", ""tileType"": ""transport"", ""ownerPlayerId"": null },
      { ""tileId"": ""tax_01"", ""index"": 10, ""displayName"": ""Tax 01"", ""tileType"": ""tax"", ""ownerPlayerId"": null },
      { ""tileId"": ""table_01"", ""index"": 11, ""displayName"": ""Table 01"", ""tileType"": ""table_deck"", ""ownerPlayerId"": null }
    ]
  },
  ""propertyStates"": [],
  ""activeAuction"": {
    ""propertyTileId"": ""auction_test"",
    ""triggeringPlayerId"": ""player-agentic"",
    ""status"": ""active"",
    ""startingBid"": 100,
    ""minimumBidIncrement"": 10,
    ""initialPreBidSeconds"": 5,
    ""bidResetSeconds"": 10,
    ""highestBid"": 250,
    ""highestBidderId"": ""player-2"",
    ""countdownDurationSeconds"": 9,
    ""timerEndsAtUtc"": ""2026-05-12T00:17:09Z"",
    ""bids"": [
      { ""bidderPlayerId"": ""player-2"", ""amount"": 250, ""placedAtUtc"": ""2026-05-12T00:17:01Z"" }
    ]
  },
  ""movement"": null,
  ""moneyDeltas"": [],
  ""propertyOwnershipChanges"": [],
  ""playerEliminations"": []
}";
    }

    private static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string OwnerJson(string ownerPlayerId)
    {
        return string.IsNullOrWhiteSpace(ownerPlayerId) ? "null" : $"\"{ownerPlayerId}\"";
    }

    private static string Chunk5SnapshotJson(bool activeAuction)
    {
        if (activeAuction)
        {
            return @"{
  ""snapshotVersion"": 1,
  ""sessionId"": ""session_chunk_5"",
  ""status"": ""in_game"",
  ""gameStatus"": ""in_progress"",
  ""serverNowUtc"": ""2026-05-11T00:00:00Z"",
  ""matchId"": ""session_chunk_5"",
  ""phase"": ""awaiting_tile_action"",
  ""winnerPlayerId"": null,
  ""startedAtUtc"": ""2026-05-11T00:00:00Z"",
  ""endedAtUtc"": null,
  ""turn"": {
    ""currentPlayerId"": ""player-agentic"",
    ""turnIndex"": 9,
    ""hasRolledThisTurn"": true,
    ""hasResolvedTileThisTurn"": true,
    ""hasExecutedTileThisTurn"": false
  },
  ""players"": [
    {
      ""playerId"": ""player-agentic"",
      ""username"": ""Agentic Player"",
      ""tokenId"": ""token_agentic"",
      ""colorId"": ""gold"",
      ""money"": 1320,
      ""currentTileId"": ""auction_test"",
      ""ownedPropertyIds"": [""property_01""],
      ""heldCardIds"": [""chance_escape""],
      ""statusEffects"": [],
      ""loan"": {
        ""totalBorrowed"": 180,
        ""currentInterestRatePercent"": 20,
        ""nextTurnInterestDue"": 36,
        ""loanTier"": 1
      },
      ""jailTurnCount"": 0,
      ""jailRollAttemptCount"": 0,
      ""consecutiveDoublesCount"": 0,
      ""lastJailReleaseReason"": null,
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    },
    {
      ""playerId"": ""player-2"",
      ""username"": ""Blue Player"",
      ""tokenId"": ""token_blue"",
      ""colorId"": ""blue"",
      ""money"": 1640,
      ""currentTileId"": ""property_01"",
      ""ownedPropertyIds"": [],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": {
        ""totalBorrowed"": 0,
        ""currentInterestRatePercent"": 0,
        ""nextTurnInterestDue"": 0,
        ""loanTier"": 0
      },
      ""jailTurnCount"": 0,
      ""jailRollAttemptCount"": 0,
      ""consecutiveDoublesCount"": 0,
      ""lastJailReleaseReason"": null,
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    }
  ],
  ""board"": {
    ""boardId"": ""chunk_5_board"",
    ""version"": 1,
    ""displayName"": ""Chunk 5 Board"",
    ""tiles"": [
      { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""groupId"": """", ""price"": 0, ""rentTable"": [], ""upgradeCost"": 0, ""isPurchasable"": false, ""isAuctionable"": false, ""ownerPlayerId"": null },
      { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""groupId"": ""group_01"", ""price"": 60, ""rentTable"": [2, 10, 30], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": ""player-agentic"" },
      { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""groupId"": ""group_01"", ""price"": 80, ""rentTable"": [4, 20, 60], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": ""player-2"" },
      { ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""groupId"": ""group_02"", ""price"": 100, ""rentTable"": [6, 30, 90], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": null }
    ]
  },
  ""propertyStates"": [
    { ""tileId"": ""property_01"", ""data"": { ""damagePercent"": 25, ""isMortgaged"": false, ""upgradeLevel"": 1 } }
  ],
  ""activeAuction"": {
    ""propertyTileId"": ""auction_test"",
    ""triggeringPlayerId"": ""player-agentic"",
    ""status"": ""active"",
    ""startingBid"": 100,
    ""minimumBidIncrement"": 10,
    ""initialPreBidSeconds"": 5,
    ""bidResetSeconds"": 10,
    ""highestBid"": 240,
    ""highestBidderId"": ""player-2"",
    ""countdownDurationSeconds"": 14,
    ""timerEndsAtUtc"": ""2026-05-11T00:00:14Z"",
    ""bids"": [
      { ""bidderPlayerId"": ""player-agentic"", ""amount"": 220, ""placedAtUtc"": ""2026-05-11T00:00:01Z"" },
      { ""bidderPlayerId"": ""player-2"", ""amount"": 240, ""placedAtUtc"": ""2026-05-11T00:00:02Z"" }
    ]
  },
  ""movement"": {
    ""playerId"": ""player-agentic"",
    ""fromTileId"": ""start"",
    ""toTileId"": ""auction_test"",
    ""pathTileIds"": [""property_01"", ""property_02"", ""auction_test""],
    ""stepCount"": 3,
    ""movementKind"": ""path"",
    ""passedStart"": false
  },
  ""moneyDeltas"": [
    { ""playerId"": ""player-agentic"", ""delta"": -30, ""balance"": 1320, ""reason"": ""rent"", ""counterpartyPlayerId"": ""player-2"", ""tileId"": ""property_02"", ""cardId"": null }
  ],
  ""propertyOwnershipChanges"": [
    { ""tileId"": ""property_01"", ""previousOwnerPlayerId"": null, ""newOwnerPlayerId"": ""player-agentic"", ""reason"": ""purchase"" }
  ],
  ""playerEliminations"": []
}";
        }

        return @"{
  ""snapshotVersion"": 1,
  ""sessionId"": ""session_chunk_5"",
  ""status"": ""in_game"",
  ""gameStatus"": ""in_progress"",
  ""serverNowUtc"": ""2026-05-11T00:00:30Z"",
  ""matchId"": ""session_chunk_5"",
  ""phase"": ""awaiting_roll"",
  ""winnerPlayerId"": null,
  ""startedAtUtc"": ""2026-05-11T00:00:00Z"",
  ""endedAtUtc"": null,
  ""turn"": {
    ""currentPlayerId"": ""player-2"",
    ""turnIndex"": 10,
    ""hasRolledThisTurn"": false,
    ""hasResolvedTileThisTurn"": false,
    ""hasExecutedTileThisTurn"": false
  },
  ""players"": [
    {
      ""playerId"": ""player-agentic"",
      ""username"": ""Agentic Player"",
      ""tokenId"": ""token_agentic"",
      ""colorId"": ""gold"",
      ""money"": 1290,
      ""currentTileId"": ""property_02"",
      ""ownedPropertyIds"": [""property_02""],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": {
        ""totalBorrowed"": 180,
        ""currentInterestRatePercent"": 20,
        ""nextTurnInterestDue"": 36,
        ""loanTier"": 1
      },
      ""jailTurnCount"": 0,
      ""jailRollAttemptCount"": 0,
      ""consecutiveDoublesCount"": 0,
      ""lastJailReleaseReason"": null,
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    },
    {
      ""playerId"": ""player-2"",
      ""username"": ""Blue Player"",
      ""tokenId"": ""token_blue"",
      ""colorId"": ""blue"",
      ""money"": 1640,
      ""currentTileId"": ""property_01"",
      ""ownedPropertyIds"": [],
      ""heldCardIds"": [],
      ""statusEffects"": [],
      ""loan"": {
        ""totalBorrowed"": 0,
        ""currentInterestRatePercent"": 0,
        ""nextTurnInterestDue"": 0,
        ""loanTier"": 0
      },
      ""jailTurnCount"": 0,
      ""jailRollAttemptCount"": 0,
      ""consecutiveDoublesCount"": 0,
      ""lastJailReleaseReason"": null,
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    }
  ],
  ""board"": {
    ""boardId"": ""chunk_5_board"",
    ""version"": 1,
    ""displayName"": ""Chunk 5 Board"",
    ""tiles"": [
      { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""groupId"": """", ""price"": 0, ""rentTable"": [], ""upgradeCost"": 0, ""isPurchasable"": false, ""isAuctionable"": false, ""ownerPlayerId"": null },
      { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""groupId"": ""group_01"", ""price"": 60, ""rentTable"": [2, 10, 30], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": null },
      { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""groupId"": ""group_01"", ""price"": 80, ""rentTable"": [4, 20, 60], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": ""player-agentic"" },
      { ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""groupId"": ""group_02"", ""price"": 100, ""rentTable"": [6, 30, 90], ""upgradeCost"": 50, ""isPurchasable"": true, ""isAuctionable"": true, ""ownerPlayerId"": null }
    ]
  },
  ""propertyStates"": [],
  ""activeAuction"": null
}";
    }

    private static string Chunk6LiveSessionUpdateSnapshotJson()
    {
        return @"{
  ""snapshotVersion"": 1,
  ""sessionId"": ""session_chunk_6_live_mock"",
  ""status"": ""in_game"",
  ""gameStatus"": ""in_progress"",
  ""serverNowUtc"": ""2026-05-11T00:01:00Z"",
  ""matchId"": ""session_chunk_6_live_mock"",
  ""phase"": ""auction_bidding"",
  ""turn"": {
    ""currentPlayerId"": ""player-agentic"",
    ""turnIndex"": 11,
    ""hasRolledThisTurn"": true,
    ""hasResolvedTileThisTurn"": true,
    ""hasExecutedTileThisTurn"": true
  },
  ""players"": [
    {
      ""playerId"": ""player-agentic"",
      ""username"": ""Agentic Player"",
      ""tokenId"": ""token_agentic"",
      ""colorId"": ""gold"",
      ""money"": 1210,
      ""currentTileId"": ""auction_test"",
      ""loan"": {
        ""totalBorrowed"": 240,
        ""currentInterestRatePercent"": 25,
        ""nextTurnInterestDue"": 60,
        ""loanTier"": 2
      },
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    },
    {
      ""playerId"": ""player-2"",
      ""username"": ""Blue Player"",
      ""tokenId"": ""token_blue"",
      ""colorId"": ""blue"",
      ""money"": 1580,
      ""currentTileId"": ""property_01"",
      ""loan"": {
        ""totalBorrowed"": 0,
        ""currentInterestRatePercent"": 0,
        ""nextTurnInterestDue"": 0,
        ""loanTier"": 0
      },
      ""isBankrupt"": false,
      ""isEliminated"": false,
      ""isLockedUp"": false
    }
  ],
  ""board"": {
    ""boardId"": ""chunk_6_board"",
    ""version"": 2,
    ""displayName"": ""Chunk 6 Live Mock Board"",
    ""tiles"": [
      { ""tileId"": ""start"", ""index"": 0, ""displayName"": ""Start"", ""tileType"": ""start"", ""ownerPlayerId"": null },
      { ""tileId"": ""property_01"", ""index"": 1, ""displayName"": ""Property 01"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-2"" },
      { ""tileId"": ""property_02"", ""index"": 2, ""displayName"": ""Property 02"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" },
      { ""tileId"": ""auction_test"", ""index"": 3, ""displayName"": ""Auction Test"", ""tileType"": ""property"", ""ownerPlayerId"": ""player-agentic"" }
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
    ""highestBid"": 310,
    ""highestBidderId"": ""player-agentic"",
    ""countdownDurationSeconds"": 7,
    ""timerEndsAtUtc"": ""2026-05-11T00:01:07Z"",
    ""bids"": [
      { ""bidderPlayerId"": ""player-2"", ""amount"": 290, ""placedAtUtc"": ""2026-05-11T00:01:01Z"" },
      { ""bidderPlayerId"": ""player-agentic"", ""amount"": 310, ""placedAtUtc"": ""2026-05-11T00:01:02Z"" }
    ]
  },
  ""movement"": {
    ""playerId"": ""player-agentic"",
    ""fromTileId"": ""property_02"",
    ""toTileId"": ""auction_test"",
    ""pathTileIds"": [""auction_test""],
    ""stepCount"": 1,
    ""movementKind"": ""snap"",
    ""passedStart"": false
  },
  ""moneyDeltas"": [
    { ""playerId"": ""player-agentic"", ""delta"": -80, ""balance"": 1210, ""reason"": ""mock_live_update"", ""counterpartyPlayerId"": ""player-2"", ""tileId"": ""auction_test"", ""cardId"": null }
  ],
  ""propertyOwnershipChanges"": [
    { ""tileId"": ""auction_test"", ""previousOwnerPlayerId"": null, ""newOwnerPlayerId"": ""player-agentic"", ""reason"": ""mock_live_update"" }
  ],
  ""playerEliminations"": []
}";
    }

    private void ValidateHud()
    {
        GameObject prefab = hudPrefab != null ? hudPrefab : LoadPrefabInEditor("Assets/Prefabs/HUDPrefab.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] HUDPrefab is not assigned.");
            return;
        }

        GameObject hudObject = Instantiate(prefab);
        HUDController hud = hudObject.GetComponentInChildren<HUDController>();
        if (hud == null)
        {
            Debug.LogError("[AgenticTestRunner] HUDPrefab is missing HUDController.", hudObject);
            return;
        }

        HUDController.PlayerHudSnapshot player = new HUDController.PlayerHudSnapshot(
            "player-agentic",
            "Agentic Player",
            "gold",
            1420,
            "property_02",
            300,
            10f,
            30,
            "starter",
            false,
            false,
            false);
        HUDController.TurnHudSnapshot turn = new HUDController.TurnHudSnapshot(
            "player-agentic",
            5,
            "AwaitingTileAction",
            true,
            true,
            false);

        hud.BindSnapshot(player, turn);
        Debug.Log($"[AgenticTestRunner] HUD bound: player={player.PlayerId}, money={player.Money}, loan={player.LoanTotalBorrowed}, turn={turn.TurnIndex}/{turn.Phase}.", hudObject);
    }

    private void ValidateTurnUi()
    {
        if (!instantiateTurnUi)
        {
            return;
        }

        GameObject prefab = turnUiPrefab != null ? turnUiPrefab : LoadPrefabInEditor("Assets/Prefabs/TurnUIPrefab.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] TurnUIPrefab is not assigned.");
            return;
        }

        GameObject turnObject = Instantiate(prefab);
        TurnController turnController = turnObject.GetComponentInChildren<TurnController>();
        if (turnController == null)
        {
            Debug.LogError("[AgenticTestRunner] TurnUIPrefab is missing TurnController.", turnObject);
            return;
        }

        DiceAnimator diceAnimator = turnObject.GetComponentInChildren<DiceAnimator>();
        Debug.Log($"[AgenticTestRunner] Turn UI inspector fields: turnController={turnController != null}, diceImages={turnController.DiceImageCount}, rollButtonInteractable={turnController.RollButtonInteractable}, diceAnimator={diceAnimator != null}, diceFaces={(diceAnimator == null ? 0 : diceAnimator.DiceFaceCount)}, rollDuration={(diceAnimator == null ? 0f : diceAnimator.RollDuration)}.", turnObject);
        Debug.Log("[AgenticTestRunner] Turn UI uses mock readonly TurnHudSnapshot data only; no backend calls are made.", turnObject);

        StartCoroutine(RunTurnUiMockFlow(turnController, diceAnimator, turnObject));
    }

    private static IEnumerator RunTurnUiMockFlow(TurnController turnController, DiceAnimator diceAnimator, GameObject turnObject)
    {
        HUDController.TurnHudSnapshot firstTurn = new HUDController.TurnHudSnapshot(
            "player-agentic",
            6,
            "AwaitingRoll",
            false,
            false,
            false);

        turnController.BindSnapshot(firstTurn);
        turnController.StartTurn();
        Debug.Log($"[AgenticTestRunner] Turn UI mock turn start: player={firstTurn.CurrentPlayerId}, turn={firstTurn.TurnIndex}, phase={firstTurn.Phase}, rollButtonInteractable={turnController.RollButtonInteractable}.", turnObject);
        turnController.RollDice();
        Debug.Log($"[AgenticTestRunner] Turn UI mock roll triggered: values={string.Join(" + ", turnController.LastRollValues)}, diceAnimating={(diceAnimator != null && diceAnimator.IsAnimating)}.", turnObject);

        while (diceAnimator != null && diceAnimator.IsAnimating)
        {
            yield return null;
        }

        Debug.Log($"[AgenticTestRunner] Turn UI dice animation end: values={string.Join(" + ", turnController.LastRollValues)}, diceAnimating={(diceAnimator != null && diceAnimator.IsAnimating)}.", turnObject);
        turnController.EndTurn();

        HUDController.TurnHudSnapshot secondTurn = new HUDController.TurnHudSnapshot(
            "player-2",
            7,
            "AwaitingRoll",
            false,
            false,
            false);

        turnController.BindSnapshot(secondTurn);
        turnController.StartTurn();
        Debug.Log($"[AgenticTestRunner] Turn UI second mock turn start: player={secondTurn.CurrentPlayerId}, turn={secondTurn.TurnIndex}, phase={secondTurn.Phase}, rollButtonInteractable={turnController.RollButtonInteractable}.", turnObject);
        turnController.RollDice();

        while (diceAnimator != null && diceAnimator.IsAnimating)
        {
            yield return null;
        }

        Debug.Log($"[AgenticTestRunner] Turn UI second mock roll complete: values={string.Join(" + ", turnController.LastRollValues)}, rollButtonInteractable={turnController.RollButtonInteractable}.", turnObject);
        turnController.EndTurn();
    }

    private void ValidatePlayerToken(IReadOnlyDictionary<string, BoardTileController> tilesById)
    {
        GameObject prefab = playerTokenPrefab != null ? playerTokenPrefab : LoadPrefabInEditor("Assets/Prefabs/PlayerToken.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] PlayerToken prefab is not assigned.");
            return;
        }

        GameObject tokenObject = Instantiate(prefab, testTilePosition, Quaternion.identity);
        PlayerTokenController token = tokenObject.GetComponent<PlayerTokenController>();
        if (token == null)
        {
            Debug.LogError("[AgenticTestRunner] PlayerToken prefab is missing PlayerTokenController.", tokenObject);
            return;
        }

        token.SetPlayer(testPlayerId, testTokenColor);
        token.SetCurrentTileIndex(testTileIndex);

        if (tilesById != null && tilesById.TryGetValue("start", out BoardTileController startTile))
        {
            token.MoveToTilePosition(startTile.GetTokenAnchorPosition(0.35f));
        }
        else
        {
            token.MoveToTilePosition(testTilePosition);
        }

        Debug.Log($"[AgenticTestRunner] PlayerToken instantiated: playerId={token.PlayerId}, currentTileIndex={token.CurrentTileIndex}, color={token.TokenColor}, tag={tokenObject.tag}, position={tokenObject.transform.position}", tokenObject);
        Debug.Log(tokenObject.CompareTag("PlayerToken")
            ? "[AgenticTestRunner] Tag validation reports PlayerToken."
            : $"[AgenticTestRunner] Tag validation failed: {tokenObject.tag}", tokenObject);

        if (tilesById == null || tilesById.Count == 0)
        {
            return;
        }

        TokenAnimator animator = tokenObject.GetComponent<TokenAnimator>();
        if (animator == null)
        {
            animator = tokenObject.AddComponent<TokenAnimator>();
        }

        List<string> path = new List<string> { "start", "property_01", "property_02", "auction_test" };
        int stepCount = 3;
        TokenAnimator.MovementKind movementKind = TokenAnimator.MovementKind.Fast;
        Debug.Log($"[AgenticTestRunner] Token animation start: path={string.Join(" -> ", path)}, stepCount={stepCount}, movementKind={movementKind}.", tokenObject);
        animator.AnimateAlongPath(path, tilesById, stepCount, movementKind);
        StartCoroutine(LogTokenAnimationCompletion(animator, tokenObject, path, stepCount, movementKind));
    }

    private void ValidateAuctionPanel()
    {
        if (!instantiateAuctionPanel)
        {
            return;
        }

        GameObject prefab = auctionPanelPrefab != null ? auctionPanelPrefab : LoadPrefabInEditor("Assets/Prefabs/AuctionPanel.prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[AgenticTestRunner] AuctionPanel prefab is not assigned.");
            return;
        }

        GameObject panelObject = Instantiate(prefab);
        AuctionPanelController panel = panelObject.GetComponentInChildren<AuctionPanelController>();
        if (panel == null)
        {
            Debug.LogError("[AgenticTestRunner] AuctionPanel prefab is missing AuctionPanelController.", panelObject);
            return;
        }

        panel.BindAuctionSnapshot("auction-agentic", 320, "player-2", "player-3", 24f);
        panel.BindPlayerBidRows(
            new List<string> { "player-1", "player-2", "player-3", "player-4" },
            new List<int> { 200, 320, 300, 0 });
        panel.SetPlayerHighlight("player-3", true);
        panel.AnimateCountdownHighlight(6f, 30f);
        panel.PulseBidHighlight("player-2");
        panel.AppendLog("Mock auction data bound locally.");

        Debug.Log("[AgenticTestRunner] Auction countdown/highlight state applied locally: remainingSeconds=6, totalSeconds=30, highBidder=player-2.", panelObject);
        Debug.Log("[AgenticTestRunner] Auction panel mock data binds without backend calls.", panelObject);
    }

    private static IEnumerator LogTokenAnimationCompletion(
        TokenAnimator animator,
        GameObject tokenObject,
        IReadOnlyList<string> path,
        int stepCount,
        TokenAnimator.MovementKind movementKind)
    {
        while (animator != null && animator.IsAnimating)
        {
            yield return null;
        }

        Debug.Log($"[AgenticTestRunner] Token animation end: path={string.Join(" -> ", path)}, stepCount={stepCount}, movementKind={movementKind}, position={tokenObject.transform.position}.", tokenObject);
    }

    private static void EnsureEventSystem()
    {
        EventSystem current = EventSystem.current;
        GameObject eventSystem = current == null
            ? new GameObject("EventSystem", typeof(EventSystem))
            : current.gameObject;

        System.Type inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModuleType != null)
        {
            StandaloneInputModule[] legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
            for (int i = 0; i < legacyModules.Length; i++)
            {
                legacyModules[i].enabled = false;
            }

            if (eventSystem.GetComponent(inputSystemModuleType) == null)
            {
                eventSystem.AddComponent(inputSystemModuleType);
            }
        }
        else if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        if (Application.isPlaying && current == null)
        {
            DontDestroyOnLoad(eventSystem);
        }

        Debug.Log($"[AgenticTestRunner] EventSystem ready for runtime UI input validation: inputModule={(inputSystemModuleType == null ? "StandaloneInputModule" : "InputSystemUIInputModule")}. Mock/read-only; no backend mutation.", eventSystem);
    }

    private static GameObject LoadPrefabInEditor(string assetPath)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
#else
        _ = assetPath;
        return null;
#endif
    }
}
