using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class AgenticTestRunner : MonoBehaviour
{
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapForSampleScene()
    {
        if (SceneManager.GetActiveScene().name != "SampleScene")
        {
            return;
        }

        if (FindAnyObjectByType<AgenticTestRunner>() != null)
        {
            return;
        }

        GameObject runner = new GameObject("AgenticTestRunner", typeof(AgenticTestRunner));
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(runner);
        }

        Debug.Log("[AgenticTestRunner] Runtime validation runner created for SampleScene.", runner);
    }

    private void Start()
    {
        EnsureEventSystem();
        Dictionary<string, BoardTileController> tilesById = ValidateBoardTiles();
        ValidateHud();
        ValidateTurnUi();
        ValidatePlayerToken(tilesById);
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
            tile.SetHighlighted(i == 3);
            tilesById[tile.TileId] = tile;

            Debug.Log($"[AgenticTestRunner] Tile bound: tileId={tile.TileId}, ownerId={tile.OwnerPlayerId}, highlighted={tile.IsHighlighted}.", tileObject);
        }

        return tilesById;
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
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(eventSystem);
        }

        Debug.Log("[AgenticTestRunner] EventSystem created for runtime UI input validation.", eventSystem);
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
