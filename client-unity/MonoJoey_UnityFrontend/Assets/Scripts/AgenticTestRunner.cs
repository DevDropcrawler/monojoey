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

        RunReadOnlySnapshotHydrationValidation(hud, turnController, token, tokenAnimator, boardPath);
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

    private void RunReadOnlySnapshotHydrationValidation(
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
            return;
        }

        AuctionPanelController auction = auctionObject.GetComponentInChildren<AuctionPanelController>();
        if (auction == null)
        {
            Debug.LogError("[AgenticTestRunner] Chunk 5 snapshot hydration skipped because AuctionPanelController was missing. Read-only snapshot hydration; no backend mutation.", auctionObject);
            return;
        }

        GameObject hydratorObject = new GameObject("SnapshotHydrator_Chunk5Runtime", typeof(SnapshotHydrator));
        SnapshotHydrator hydrator = hydratorObject.GetComponent<SnapshotHydrator>();
        hydrator.Configure(hud, turnController, auction, token, tokenAnimator, boardPath, testPlayerId);

        bool firstHydrated = hydrator.HydrateSnapshotJson(Chunk5SnapshotJson(activeAuction: true));
        BoardTileController propertyOne = FindBoardTile(boardPath, "property_01");
        Debug.Log($"[AgenticTestRunner] Chunk 5 first snapshot hydrated={firstHydrated}: hudMoney={hud.LastPlayerSnapshot.Money}, hudLoan={hud.LastPlayerSnapshot.LoanTotalBorrowed}, hudTile={hud.LastPlayerSnapshot.CurrentTileId}, turnPhase={turnController.LastSnapshot.Phase}, hasRolled={turnController.LastSnapshot.HasRolledThisTurn}, hasResolved={turnController.LastSnapshot.HasResolvedTileThisTurn}, auctionHighBidder={auction.HighBidderPlayerId}, auctionHighBid={auction.CurrentHighBid}, tileOwner={propertyOne?.OwnerPlayerId}, tileIndex={(propertyOne == null ? -1 : propertyOne.BoardIndex)}, tokenPlayer={token.PlayerId}, tokenTileIndex={token.CurrentTileIndex}, tokenPosition={token.transform.position}. Read-only snapshot hydration; no backend mutation.", hydratorObject);

        bool secondHydrated = hydrator.HydrateSnapshotJson(Chunk5SnapshotJson(activeAuction: false));
        BoardTileController propertyTwo = FindBoardTile(boardPath, "property_02");
        Debug.Log($"[AgenticTestRunner] Chunk 5 second snapshot hydrated={secondHydrated}: auctionId={auction.AuctionId}, auctionHighBidder={auction.HighBidderPlayerId}, auctionHighBid={auction.CurrentHighBid}, helpers movement={(hydrator.LastMovement == null ? "cleared" : "present")}, moneyDeltas={hydrator.LastMoneyDeltas.Length}, ownershipChanges={hydrator.LastPropertyOwnershipChanges.Length}, eliminations={hydrator.LastPlayerEliminations.Length}, tileOwner={propertyTwo?.OwnerPlayerId}, tileIndex={(propertyTwo == null ? -1 : propertyTwo.BoardIndex)}, tokenPlayer={token.PlayerId}, tokenTileId={hydrator.LastHydratedTileId}, tokenTileIndex={token.CurrentTileIndex}, tokenPosition={token.transform.position}. Read-only snapshot hydration; no backend mutation.", hydratorObject);
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
