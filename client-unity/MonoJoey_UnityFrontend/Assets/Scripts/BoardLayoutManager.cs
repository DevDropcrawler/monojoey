using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BoardLayoutManager : MonoBehaviour
{
    [SerializeField] private GameObject tilePrefab;
    [SerializeField] private GameObject playerTokenPrefab;
    [SerializeField] private Transform tileRoot;
    [SerializeField] private Transform tokenRoot;
    [SerializeField] private float boardWidth = 12f;
    [SerializeField] private float boardDepth = 12f;
    [SerializeField] private float tokenYOffset = 0.35f;
    [SerializeField] private bool enableDebugLogging;

    private readonly Dictionary<string, BoardTileController> tilesById = new Dictionary<string, BoardTileController>();
    private readonly Dictionary<string, PlayerTokenController> tokensByPlayerId = new Dictionary<string, PlayerTokenController>();
    private readonly Dictionary<string, int> tokenSlotByPlayerId = new Dictionary<string, int>();
    private readonly Dictionary<string, int> tokenCountByTileId = new Dictionary<string, int>();
    private readonly List<BoardTileController> orderedTiles = new List<BoardTileController>();

    private PlayerTokenController externalSelectedToken;
    private Transform boardSurfaceRoot;
    private Renderer boardSurfaceRenderer;
    private string selectedPlayerId = "";
    private string selectedTileId = "";
    private string currentPlayerTileId = "";
    private string activeAuctionTileId = "";
    private Bounds boardBounds;

    public int TileCount => orderedTiles.Count;
    public int TokenCount => tokensByPlayerId.Count + (externalSelectedToken == null ? 0 : 1);
    public string SelectedTileId => selectedTileId;
    public string CurrentPlayerTileId => currentPlayerTileId;
    public string ActiveAuctionTileId => activeAuctionTileId;
    public Bounds BoardBounds => boardBounds;
    public BoardTileController[] RuntimeTiles => orderedTiles.ToArray();
    public bool HasPlaceholderBoardSurface => boardSurfaceRenderer != null;
    public Vector3 BoardSurfaceScale => boardSurfaceRenderer == null ? Vector3.zero : boardSurfaceRenderer.transform.localScale;

    public void Configure(GameObject newTilePrefab, GameObject newPlayerTokenPrefab)
    {
        if (newTilePrefab != null)
        {
            tilePrefab = newTilePrefab;
        }

        if (newPlayerTokenPrefab != null)
        {
            playerTokenPrefab = newPlayerTokenPrefab;
        }
    }

    public void SetExternalSelectedToken(PlayerTokenController token)
    {
        externalSelectedToken = token;
    }

    public void BindSnapshot(MonoJoeySnapshot snapshot, string newSelectedPlayerId)
    {
        selectedPlayerId = string.IsNullOrWhiteSpace(newSelectedPlayerId) ? "" : newSelectedPlayerId;
        EnsureRoots();
        BindTiles(snapshot);
        CaptureTokenSlots(snapshot == null ? null : snapshot.players);
        CaptureHighlightContext(snapshot);
        ApplyTileHighlights();
        BindTokens(snapshot == null ? null : snapshot.players);
        CaptureBoardBounds();
        LogDebug($"Snapshot board bound: tiles={TileCount}, tokens={TokenCount}, selected={Display(selectedTileId)}, current={Display(currentPlayerTileId)}, auction={Display(activeAuctionTileId)}, bounds={boardBounds}.");
    }

    public bool TryGetTile(string tileId, out BoardTileController tile)
    {
        if (string.IsNullOrWhiteSpace(tileId))
        {
            tile = null;
            return false;
        }

        return tilesById.TryGetValue(tileId, out tile) && tile != null;
    }

    public bool TryGetToken(string playerId, out PlayerTokenController token)
    {
        if (string.Equals(playerId, selectedPlayerId, StringComparison.Ordinal) && externalSelectedToken != null)
        {
            token = externalSelectedToken;
            return true;
        }

        if (string.IsNullOrWhiteSpace(playerId))
        {
            token = null;
            return false;
        }

        return tokensByPlayerId.TryGetValue(playerId, out token) && token != null;
    }

    public Vector3 GetTokenAnchorPosition(string tileId, string playerId, float fallbackYOffset)
    {
        if (!TryGetTile(tileId, out BoardTileController tile))
        {
            return Vector3.zero;
        }

        int slotIndex = tokenSlotByPlayerId.TryGetValue(playerId ?? "", out int capturedSlotIndex) ? capturedSlotIndex : 0;
        int slotCount = tokenCountByTileId.TryGetValue(tileId ?? "", out int capturedSlotCount) ? capturedSlotCount : 1;
        return tile.GetTokenAnchorPosition(fallbackYOffset, slotIndex, slotCount);
    }

    private void BindTiles(MonoJoeySnapshot snapshot)
    {
        orderedTiles.Clear();
        tilesById.Clear();

        MonoJoeyBoardTileSnapshot[] snapshotTiles = snapshot == null || snapshot.board == null
            ? null
            : snapshot.board.tiles;
        if (snapshotTiles == null || snapshotTiles.Length == 0 || tilePrefab == null)
        {
            return;
        }

        MonoJoeyBoardTileSnapshot[] sortedTiles = CopyAndSortTiles(snapshotTiles);
        for (int i = 0; i < sortedTiles.Length; i++)
        {
            MonoJoeyBoardTileSnapshot snapshotTile = sortedTiles[i];
            if (snapshotTile == null || string.IsNullOrWhiteSpace(snapshotTile.tileId))
            {
                continue;
            }

            BoardTileController tile = EnsureTile(i);
            if (tile == null)
            {
                continue;
            }

            tile.gameObject.name = $"Tile_{snapshotTile.index:D2}_{snapshotTile.tileId}";
            tile.transform.SetParent(tileRoot, false);
            tile.transform.localPosition = PositionForIndex(i, sortedTiles.Length);
            tile.transform.localRotation = RotationForPosition(tile.transform.localPosition);
            tile.BindTileSnapshot(snapshotTile, FallbackOwnerColor(snapshotTile.ownerPlayerId));
            tile.SetBoardIndex(snapshotTile.index);

            orderedTiles.Add(tile);
            tilesById[snapshotTile.tileId] = tile;
        }

        DisableExtraTiles(orderedTiles.Count);
    }

    private BoardTileController EnsureTile(int index)
    {
        if (tileRoot != null && index < tileRoot.childCount)
        {
            BoardTileController existing = tileRoot.GetChild(index).GetComponent<BoardTileController>();
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }
        }

        GameObject tileObject = Instantiate(tilePrefab, tileRoot);
        tileObject.hideFlags = HideFlags.DontSave;
        return tileObject.GetComponent<BoardTileController>();
    }

    private void DisableExtraTiles(int activeCount)
    {
        if (tileRoot == null)
        {
            return;
        }

        for (int i = activeCount; i < tileRoot.childCount; i++)
        {
            tileRoot.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void CaptureTokenSlots(MonoJoeyPlayerSnapshot[] players)
    {
        tokenSlotByPlayerId.Clear();
        tokenCountByTileId.Clear();
        Dictionary<string, int> nextSlotByTileId = new Dictionary<string, int>();
        if (players == null)
        {
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            MonoJoeyPlayerSnapshot player = players[i];
            if (player == null || string.IsNullOrWhiteSpace(player.playerId) || string.IsNullOrWhiteSpace(player.currentTileId))
            {
                continue;
            }

            string tileId = player.currentTileId;
            int nextSlot = nextSlotByTileId.TryGetValue(tileId, out int value) ? value : 0;
            tokenSlotByPlayerId[player.playerId] = nextSlot;
            nextSlotByTileId[tileId] = nextSlot + 1;
            tokenCountByTileId[tileId] = nextSlot + 1;
        }
    }

    private void CaptureHighlightContext(MonoJoeySnapshot snapshot)
    {
        selectedTileId = TileForPlayer(snapshot, selectedPlayerId);
        currentPlayerTileId = TileForPlayer(snapshot, snapshot == null || snapshot.turn == null ? "" : snapshot.turn.currentPlayerId);
        activeAuctionTileId = snapshot == null || snapshot.activeAuction == null ? "" : snapshot.activeAuction.propertyTileId;
    }

    private void ApplyTileHighlights()
    {
        for (int i = 0; i < orderedTiles.Count; i++)
        {
            BoardTileController tile = orderedTiles[i];
            if (tile == null)
            {
                continue;
            }

            tile.SetHighlightKind(HighlightForTile(tile.TileId));
        }
    }

    private BoardTileController.HighlightKind HighlightForTile(string tileId)
    {
        if (!string.IsNullOrWhiteSpace(activeAuctionTileId) && string.Equals(tileId, activeAuctionTileId, StringComparison.Ordinal))
        {
            return BoardTileController.HighlightKind.ActiveAuction;
        }

        if (!string.IsNullOrWhiteSpace(currentPlayerTileId) && string.Equals(tileId, currentPlayerTileId, StringComparison.Ordinal))
        {
            return BoardTileController.HighlightKind.CurrentPlayer;
        }

        if (!string.IsNullOrWhiteSpace(selectedTileId) && string.Equals(tileId, selectedTileId, StringComparison.Ordinal))
        {
            return BoardTileController.HighlightKind.Selected;
        }

        return BoardTileController.HighlightKind.None;
    }

    private void BindTokens(MonoJoeyPlayerSnapshot[] players)
    {
        if (players == null)
        {
            return;
        }

        HashSet<string> activePlayerIds = new HashSet<string>();
        for (int i = 0; i < players.Length; i++)
        {
            MonoJoeyPlayerSnapshot player = players[i];
            if (player == null || string.IsNullOrWhiteSpace(player.playerId))
            {
                continue;
            }

            activePlayerIds.Add(player.playerId);
            PlayerTokenController token = TokenForPlayer(player);
            if (token == null)
            {
                continue;
            }

            token.SetPlayer(player.playerId, ColorForPlayer(player));
            if (TryGetTile(player.currentTileId, out BoardTileController tile))
            {
                int boardIndex = tile.BoardIndex >= 0 ? tile.BoardIndex : 0;
                token.SetCurrentTile(player.currentTileId, boardIndex);
                token.MoveToTilePosition(GetTokenAnchorPosition(player.currentTileId, player.playerId, tokenYOffset));
            }
        }

        DisableMissingTokens(activePlayerIds);
    }

    private PlayerTokenController TokenForPlayer(MonoJoeyPlayerSnapshot player)
    {
        if (player == null)
        {
            return null;
        }

        if (string.Equals(player.playerId, selectedPlayerId, StringComparison.Ordinal) && externalSelectedToken != null)
        {
            return externalSelectedToken;
        }

        if (tokensByPlayerId.TryGetValue(player.playerId, out PlayerTokenController existing) && existing != null)
        {
            existing.gameObject.SetActive(true);
            return existing;
        }

        if (playerTokenPrefab == null || tokenRoot == null)
        {
            return null;
        }

        GameObject tokenObject = Instantiate(playerTokenPrefab, tokenRoot);
        tokenObject.hideFlags = HideFlags.DontSave;
        tokenObject.name = $"Token_{player.playerId}";
        PlayerTokenController token = tokenObject.GetComponent<PlayerTokenController>();
        if (token == null)
        {
            Destroy(tokenObject);
            return null;
        }

        tokensByPlayerId[player.playerId] = token;
        return token;
    }

    private void DisableMissingTokens(HashSet<string> activePlayerIds)
    {
        foreach (KeyValuePair<string, PlayerTokenController> pair in tokensByPlayerId)
        {
            if (pair.Value != null && !activePlayerIds.Contains(pair.Key))
            {
                pair.Value.gameObject.SetActive(false);
            }
        }
    }

    private void CaptureBoardBounds()
    {
        bool hasBounds = false;
        boardBounds = new Bounds(transform.position, Vector3.zero);
        for (int i = 0; i < orderedTiles.Count; i++)
        {
            BoardTileController tile = orderedTiles[i];
            if (tile == null)
            {
                continue;
            }

            Renderer tileRenderer = tile.GetComponentInChildren<Renderer>();
            Bounds tileBounds = tileRenderer == null ? new Bounds(tile.transform.position, Vector3.one) : tileRenderer.bounds;
            if (!hasBounds)
            {
                boardBounds = tileBounds;
                hasBounds = true;
            }
            else
            {
                boardBounds.Encapsulate(tileBounds);
            }
        }
    }

    private void EnsureRoots()
    {
        if (tileRoot == null)
        {
            tileRoot = new GameObject("BoardTiles_Runtime").transform;
            tileRoot.SetParent(transform, false);
            tileRoot.gameObject.hideFlags = HideFlags.DontSave;
        }

        if (tokenRoot == null)
        {
            tokenRoot = new GameObject("BoardTokens_Runtime").transform;
            tokenRoot.SetParent(transform, false);
            tokenRoot.gameObject.hideFlags = HideFlags.DontSave;
        }

        EnsureBoardSurface();
    }

    private void EnsureBoardSurface()
    {
        if (boardSurfaceRoot != null || !Application.isPlaying)
        {
            return;
        }

        boardSurfaceRoot = new GameObject("BoardSurface_Runtime").transform;
        boardSurfaceRoot.SetParent(transform, false);
        boardSurfaceRoot.gameObject.hideFlags = HideFlags.DontSave;

        GameObject surface = PlaceholderVisualTheme.CreateRuntimeCubeChild(
            boardSurfaceRoot,
            "BoardTableSurface_Runtime",
            new Vector3(0f, -0.16f, 0f),
            new Vector3(Mathf.Max(1f, boardWidth) + 3.2f, 0.12f, Mathf.Max(1f, boardDepth) + 3.2f),
            PlaceholderVisualTheme.BoardSurface);
        boardSurfaceRenderer = surface.GetComponent<Renderer>();

        float railHeight = 0.28f;
        float railThickness = 0.35f;
        float railY = 0.02f;
        float width = Mathf.Max(1f, boardWidth) + 3.5f;
        float depth = Mathf.Max(1f, boardDepth) + 3.5f;
        PlaceholderVisualTheme.CreateRuntimeCubeChild(boardSurfaceRoot, "BoardRailNorth_Runtime", new Vector3(0f, railY, depth * 0.5f), new Vector3(width, railHeight, railThickness), PlaceholderVisualTheme.BoardRail);
        PlaceholderVisualTheme.CreateRuntimeCubeChild(boardSurfaceRoot, "BoardRailSouth_Runtime", new Vector3(0f, railY, -depth * 0.5f), new Vector3(width, railHeight, railThickness), PlaceholderVisualTheme.BoardRail);
        PlaceholderVisualTheme.CreateRuntimeCubeChild(boardSurfaceRoot, "BoardRailEast_Runtime", new Vector3(width * 0.5f, railY, 0f), new Vector3(railThickness, railHeight, depth), PlaceholderVisualTheme.BoardRail);
        PlaceholderVisualTheme.CreateRuntimeCubeChild(boardSurfaceRoot, "BoardRailWest_Runtime", new Vector3(-width * 0.5f, railY, 0f), new Vector3(railThickness, railHeight, depth), PlaceholderVisualTheme.BoardRail);
    }

    private Vector3 PositionForIndex(int index, int count)
    {
        if (count <= 1)
        {
            return Vector3.zero;
        }

        float halfWidth = Mathf.Max(1f, boardWidth) * 0.5f;
        float halfDepth = Mathf.Max(1f, boardDepth) * 0.5f;
        float width = halfWidth * 2f;
        float depth = halfDepth * 2f;
        float perimeter = (width * 2f) + (depth * 2f);
        float distance = perimeter * index / count;

        if (distance <= width)
        {
            return new Vector3(-halfWidth + distance, 0f, -halfDepth);
        }

        distance -= width;
        if (distance <= depth)
        {
            return new Vector3(halfWidth, 0f, -halfDepth + distance);
        }

        distance -= depth;
        if (distance <= width)
        {
            return new Vector3(halfWidth - distance, 0f, halfDepth);
        }

        distance -= width;
        return new Vector3(-halfWidth, 0f, halfDepth - distance);
    }

    private Quaternion RotationForPosition(Vector3 position)
    {
        float halfWidth = Mathf.Max(1f, boardWidth) * 0.5f;
        float halfDepth = Mathf.Max(1f, boardDepth) * 0.5f;
        if (Mathf.Approximately(position.z, -halfDepth))
        {
            return Quaternion.identity;
        }

        if (Mathf.Approximately(position.x, halfWidth))
        {
            return Quaternion.Euler(0f, -90f, 0f);
        }

        if (Mathf.Approximately(position.z, halfDepth))
        {
            return Quaternion.Euler(0f, 180f, 0f);
        }

        return Quaternion.Euler(0f, 90f, 0f);
    }

    private static MonoJoeyBoardTileSnapshot[] CopyAndSortTiles(MonoJoeyBoardTileSnapshot[] source)
    {
        MonoJoeyBoardTileSnapshot[] copy = new MonoJoeyBoardTileSnapshot[source.Length];
        Array.Copy(source, copy, source.Length);
        Array.Sort(copy, CompareTiles);
        return copy;
    }

    private static int CompareTiles(MonoJoeyBoardTileSnapshot left, MonoJoeyBoardTileSnapshot right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int indexCompare = left.index.CompareTo(right.index);
        return indexCompare != 0 ? indexCompare : string.CompareOrdinal(left.tileId, right.tileId);
    }

    private static string TileForPlayer(MonoJoeySnapshot snapshot, string playerId)
    {
        if (snapshot == null || snapshot.players == null || string.IsNullOrWhiteSpace(playerId))
        {
            return "";
        }

        for (int i = 0; i < snapshot.players.Length; i++)
        {
            MonoJoeyPlayerSnapshot player = snapshot.players[i];
            if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal))
            {
                return player.currentTileId ?? "";
            }
        }

        return "";
    }

    private static Color ColorForPlayer(MonoJoeyPlayerSnapshot player)
    {
        return player == null ? Color.white : ColorForId(player.colorId, player.playerId);
    }

    private static Color FallbackOwnerColor(string ownerPlayerId)
    {
        return string.IsNullOrWhiteSpace(ownerPlayerId) ? Color.clear : ColorForId(ownerPlayerId, ownerPlayerId);
    }

    private static Color ColorForId(string colorId, string fallbackId)
    {
        string normalized = string.IsNullOrWhiteSpace(colorId) ? "" : colorId.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "gold":
            case "yellow":
            case "color_player_1":
                return new Color(0.95f, 0.70f, 0.18f, 1f);
            case "blue":
            case "color_player_2":
                return new Color(0.20f, 0.55f, 0.85f, 1f);
            case "green":
            case "color_player_3":
                return new Color(0.25f, 0.70f, 0.42f, 1f);
            case "red":
            case "color_player_4":
                return new Color(0.95f, 0.24f, 0.18f, 1f);
            default:
                int hash = string.IsNullOrWhiteSpace(fallbackId) ? 0 : fallbackId.GetHashCode();
                float hue = Mathf.Abs(hash % 360) / 360f;
                return Color.HSVToRGB(hue, 0.65f, 0.90f);
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BoardLayoutManager] {message}", this);
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
