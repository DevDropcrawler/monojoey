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

    [Header("Chunk 9 Optional Live Backend Smoke")]
    [SerializeField] private bool runChunk9LiveBackendSmoke;
    [SerializeField] private bool runChunk9LiveRollSmoke;
    [SerializeField] private string chunk9LiveWebSocketUrl = "ws://127.0.0.1:5000/ws";
    [SerializeField] private string chunk9LiveSessionId = "";
    [SerializeField] private string chunk9LivePlayerId = "";

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
        yield return RunOptionalChunk9LiveBackendSmoke(hydrator);
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
