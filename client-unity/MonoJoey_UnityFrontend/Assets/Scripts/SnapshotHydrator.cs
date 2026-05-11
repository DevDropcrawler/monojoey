using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SnapshotHydrator : MonoBehaviour
{
    public readonly struct HydrationHookEvent
    {
        public HydrationHookEvent(
            string hookName,
            string sessionId,
            string playerId,
            string tileId,
            string phase,
            int turnIndex,
            bool succeeded,
            string message)
        {
            UtcTimestamp = DateTime.UtcNow;
            HookName = hookName;
            SessionId = sessionId;
            PlayerId = playerId;
            TileId = tileId;
            Phase = phase;
            TurnIndex = turnIndex;
            Succeeded = succeeded;
            Message = message;
        }

        public DateTime UtcTimestamp { get; }
        public string HookName { get; }
        public string SessionId { get; }
        public string PlayerId { get; }
        public string TileId { get; }
        public string Phase { get; }
        public int TurnIndex { get; }
        public bool Succeeded { get; }
        public string Message { get; }
    }

    [SerializeField] private HUDController hudController;
    [SerializeField] private TurnController turnController;
    [SerializeField] private AuctionPanelController auctionPanelController;
    [SerializeField] private PlayerTokenController playerTokenController;
    [SerializeField] private TokenAnimator tokenAnimator;
    [SerializeField] private BoardTileController[] boardTiles = Array.Empty<BoardTileController>();
    [SerializeField] private string localPlayerId = "";
    [SerializeField] private bool logHydration = true;

    public MonoJoeySnapshot LastSnapshot { get; private set; }
    public int LastHydratedSnapshotVersion { get; private set; }
    public string LastHydratedSessionId { get; private set; } = "";
    public string LastHydratedServerNowUtc { get; private set; } = "";
    public string LastHydratedPhase { get; private set; } = "";
    public int LastHydratedTurnIndex { get; private set; } = -1;
    public string LastHydratedPlayerId { get; private set; } = "";
    public string LastHydratedTileId { get; private set; } = "";
    public bool LastHydrationSucceeded { get; private set; }
    public string LastHydrationSourceMessageType { get; private set; } = "";
    public DateTime LastHydrationUtc { get; private set; } = DateTime.MinValue;
    public MonoJoeyMovementPayload LastMovement { get; private set; }
    public MonoJoeyMoneyDeltaPayload[] LastMoneyDeltas { get; private set; } = Array.Empty<MonoJoeyMoneyDeltaPayload>();
    public MonoJoeyPropertyOwnershipChangePayload[] LastPropertyOwnershipChanges { get; private set; } = Array.Empty<MonoJoeyPropertyOwnershipChangePayload>();
    public MonoJoeyPlayerEliminationPayload[] LastPlayerEliminations { get; private set; } = Array.Empty<MonoJoeyPlayerEliminationPayload>();
    public bool LastHydratedHasActiveAuction { get; private set; }

    public event Action<HydrationHookEvent> HydrationStarted;
    public event Action<HydrationHookEvent> HudUpdated;
    public event Action<HydrationHookEvent> TurnUpdated;
    public event Action<HydrationHookEvent> AuctionUpdated;
    public event Action<HydrationHookEvent> BoardTileUpdated;
    public event Action<HydrationHookEvent> TokenUpdated;
    public event Action<HydrationHookEvent> HydrationCompleted;
    public event Action<HydrationHookEvent> HydrationFailed;

    public void Configure(
        HUDController hud,
        TurnController turn,
        AuctionPanelController auction,
        PlayerTokenController token,
        TokenAnimator animator,
        BoardTileController[] tiles,
        string configuredLocalPlayerId)
    {
        hudController = hud;
        turnController = turn;
        auctionPanelController = auction;
        playerTokenController = token;
        tokenAnimator = animator;
        boardTiles = tiles ?? Array.Empty<BoardTileController>();

        if (!string.IsNullOrWhiteSpace(configuredLocalPlayerId))
        {
            localPlayerId = configuredLocalPlayerId;
        }

        Log($"Chunk 5 hydrator configured with hud={hudController != null}, turn={turnController != null}, auction={auctionPanelController != null}, token={playerTokenController != null}, animator={tokenAnimator != null}, boardTiles={boardTiles.Length}.");
    }

    public bool HydrateSnapshotJson(string json)
    {
        LastHydrationSourceMessageType = "snapshot_json";
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("Snapshot JSON was empty.");
        }

        try
        {
            return HydrateSnapshot(JsonUtility.FromJson<MonoJoeySnapshot>(json));
        }
        catch (Exception ex)
        {
            return Fail($"Snapshot JSON parse failed: {ex.Message}");
        }
    }

    public bool HydrateSnapshot(MonoJoeySnapshot snapshot)
    {
        ClearFreshSnapshotState();
        if (snapshot == null)
        {
            return Fail("Snapshot was null.");
        }

        LastSnapshot = snapshot;
        LastHydratedSnapshotVersion = snapshot.snapshotVersion;
        LastHydratedSessionId = snapshot.sessionId ?? "";
        LastHydratedServerNowUtc = snapshot.serverNowUtc ?? "";
        LastHydratedPhase = snapshot.phase ?? "";
        LastHydratedTurnIndex = snapshot.turn == null ? -1 : snapshot.turn.turnIndex;
        Emit(HydrationStarted, "hydration-started", "", "", true, "Snapshot hydration started.");
        LastMovement = snapshot.movement;
        LastMoneyDeltas = snapshot.moneyDeltas ?? Array.Empty<MonoJoeyMoneyDeltaPayload>();
        LastPropertyOwnershipChanges = snapshot.propertyOwnershipChanges ?? Array.Empty<MonoJoeyPropertyOwnershipChangePayload>();
        LastPlayerEliminations = snapshot.playerEliminations ?? Array.Empty<MonoJoeyPlayerEliminationPayload>();
        LastHydratedHasActiveAuction = snapshot.activeAuction != null;

        MonoJoeyTurnSnapshot turn = snapshot.turn ?? new MonoJoeyTurnSnapshot();
        MonoJoeyPlayerSnapshot player = SelectPlayer(snapshot.players);
        if (tokenAnimator != null)
        {
            tokenAnimator.StopMovement(false);
        }

        if (player == null)
        {
            BindAuction(snapshot.activeAuction);
            BindBoard(snapshot);
            LastHydrationSucceeded = false;
            Emit(HydrationCompleted, "hydration-completed", "", "", false, "Snapshot had no players; board and auction hydrated only.");
            LogWarning("Snapshot had no players; board and auction were hydrated, HUD/token were skipped.");
            return false;
        }

        HUDController.TurnHudSnapshot turnHud = ToTurnHudSnapshot(turn, snapshot.phase);
        HUDController.PlayerHudSnapshot playerHud = ToPlayerHudSnapshot(player);

        if (hudController != null)
        {
            hudController.BindSnapshot(playerHud, turnHud);
            Emit(HudUpdated, "hud-updated", playerHud.PlayerId, playerHud.CurrentTileId, true, $"HUD updated: money={playerHud.Money}, loan={playerHud.LoanTotalBorrowed}.");
        }

        if (turnController != null)
        {
            turnController.BindHudSnapshot(playerHud, turnHud);
            turnController.BindTurnActionContext(snapshot.activeAuction != null);
            Emit(TurnUpdated, "turn-updated", turnHud.CurrentPlayerId, playerHud.CurrentTileId, true, $"Turn UI updated: turn={turnHud.TurnIndex}, phase={Display(turnHud.Phase)}.");
        }

        BindAuction(snapshot.activeAuction);
        BindBoard(snapshot);
        SnapToken(player, BuildRuntimeTileLookup());

        LastHydratedPlayerId = player.playerId ?? "";
        LastHydrationSucceeded = true;
        LastHydrationUtc = DateTime.UtcNow;
        Emit(HydrationCompleted, "hydration-completed", LastHydratedPlayerId, LastHydratedTileId, true, "Snapshot hydration completed.");
        Log($"Chunk 5 snapshot hydrated: session={Display(snapshot.sessionId)}, player={Display(LastHydratedPlayerId)}, tile={Display(LastHydratedTileId)}, turn={turnHud.TurnIndex}/{Display(turnHud.Phase)}, activeAuction={snapshot.activeAuction != null}, moneyDeltas={LastMoneyDeltas.Length}, ownershipChanges={LastPropertyOwnershipChanges.Length}, eliminations={LastPlayerEliminations.Length}.");
        return true;
    }

    public bool RefreshFromLiveSessionSnapshot(MonoJoeySnapshot snapshot)
    {
        return HydrateSnapshot(snapshot);
    }

    public bool RefreshFromLiveSessionSnapshotJson(string json)
    {
        return HydrateSnapshotJson(json);
    }

    public bool HydrateSnapshotResultJson(string json)
    {
        LastHydrationSourceMessageType = "snapshot_result";
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("Snapshot result JSON was empty.");
        }

        try
        {
            MonoJoeySnapshotResultEnvelope envelope = JsonUtility.FromJson<MonoJoeySnapshotResultEnvelope>(json);
            return HydrateSnapshot(envelope == null ? null : envelope.payload);
        }
        catch (Exception ex)
        {
            return Fail($"Snapshot result JSON parse failed: {ex.Message}");
        }
    }

    public bool HydrateReconnectResultJson(string json)
    {
        LastHydrationSourceMessageType = "reconnect_result";
        if (string.IsNullOrWhiteSpace(json))
        {
            return Fail("Reconnect result JSON was empty.");
        }

        try
        {
            MonoJoeyReconnectResultEnvelope envelope = JsonUtility.FromJson<MonoJoeyReconnectResultEnvelope>(json);
            return HydrateSnapshot(envelope == null || envelope.payload == null ? null : envelope.payload.snapshot);
        }
        catch (Exception ex)
        {
            return Fail($"Reconnect result JSON parse failed: {ex.Message}");
        }
    }

    private void BindAuction(MonoJoeyActiveAuctionSnapshot auction)
    {
        if (auctionPanelController == null)
        {
            return;
        }

        if (auction == null)
        {
            auctionPanelController.BindAuctionSnapshot("", 0, "", "", 0f);
            auctionPanelController.BindPlayerBidRows(Array.Empty<string>(), Array.Empty<int>());
            auctionPanelController.AppendLog("Active auction cleared from authoritative snapshot. Read-only snapshot hydration; no backend mutation.");
            Emit(AuctionUpdated, "auction-updated", "", "", true, "Active auction cleared.");
            return;
        }

        auctionPanelController.BindAuctionSnapshot(
            auction.propertyTileId,
            auction.highestBid,
            auction.highestBidderId,
            auction.triggeringPlayerId,
            auction.countdownDurationSeconds);

        MonoJoeyAuctionBidSnapshot[] bids = auction.bids ?? Array.Empty<MonoJoeyAuctionBidSnapshot>();
        List<string> playerIds = new List<string>(bids.Length);
        List<int> amounts = new List<int>(bids.Length);
        for (int i = 0; i < bids.Length; i++)
        {
            if (bids[i] == null)
            {
                continue;
            }

            playerIds.Add(bids[i].bidderPlayerId);
            amounts.Add(bids[i].amount);
        }

        auctionPanelController.BindPlayerBidRows(playerIds, amounts);
        auctionPanelController.AppendLog($"Active auction hydrated for {Display(auction.propertyTileId)}. Read-only snapshot hydration; no backend mutation.");
        Emit(AuctionUpdated, "auction-updated", auction.highestBidderId, auction.propertyTileId, true, $"Auction updated: highBid={auction.highestBid}, highBidder={Display(auction.highestBidderId)}.");
    }

    private void BindBoard(MonoJoeySnapshot snapshot)
    {
        Dictionary<string, MonoJoeyBoardTileSnapshot> snapshotTiles = BuildSnapshotTileLookup(snapshot);
        Dictionary<string, MonoJoeyPlayerSnapshot> players = BuildPlayerLookup(snapshot.players);

        for (int i = 0; i < boardTiles.Length; i++)
        {
            BoardTileController runtimeTile = boardTiles[i];
            if (runtimeTile == null)
            {
                continue;
            }

            if (!snapshotTiles.TryGetValue(runtimeTile.TileId, out MonoJoeyBoardTileSnapshot snapshotTile) || snapshotTile == null)
            {
                runtimeTile.SetOwnership("", Color.clear);
                runtimeTile.SetHighlighted(false);
                Emit(BoardTileUpdated, "board-tile-updated", "", runtimeTile.TileId, false, "Runtime board tile missing from snapshot; ownership/highlight cleared.");
                LogWarning($"Runtime board tile {Display(runtimeTile.TileId)} was missing from snapshot; cleared read-only ownership/highlight.");
                continue;
            }

            Color ownerColor = Color.clear;
            if (!string.IsNullOrWhiteSpace(snapshotTile.ownerPlayerId))
            {
                if (players.TryGetValue(snapshotTile.ownerPlayerId, out MonoJoeyPlayerSnapshot owner))
                {
                    ownerColor = ColorForPlayer(owner);
                }
                else
                {
                    LogWarning($"Snapshot tile {Display(snapshotTile.tileId)} references missing owner {Display(snapshotTile.ownerPlayerId)}; ownership id is retained with fallback color.");
                    ownerColor = FallbackOwnerColor(snapshotTile.ownerPlayerId);
                }
            }

            runtimeTile.BindTile(snapshotTile.tileId, snapshotTile.ownerPlayerId, ownerColor);
            runtimeTile.SetBoardIndex(snapshotTile.index);
            runtimeTile.SetHighlighted(false);
            Emit(BoardTileUpdated, "board-tile-updated", snapshotTile.ownerPlayerId, snapshotTile.tileId, true, $"Board tile updated: owner={Display(snapshotTile.ownerPlayerId)}, index={snapshotTile.index}.");
        }
    }

    private void SnapToken(MonoJoeyPlayerSnapshot player, IReadOnlyDictionary<string, BoardTileController> tilesById)
    {
        if (playerTokenController == null || player == null)
        {
            return;
        }

        playerTokenController.SetPlayer(player.playerId, ColorForPlayer(player));

        if (tilesById != null
            && !string.IsNullOrWhiteSpace(player.currentTileId)
            && tilesById.TryGetValue(player.currentTileId, out BoardTileController tile)
            && tile != null)
        {
            int boardIndex = tile.BoardIndex >= 0 ? tile.BoardIndex : 0;
            playerTokenController.SetCurrentTileIndex(boardIndex);
            playerTokenController.MoveToTilePosition(tile.GetTokenAnchorPosition(0.35f));
            LastHydratedTileId = player.currentTileId;
            Emit(TokenUpdated, "token-updated", player.playerId, player.currentTileId, true, $"Token snapped: boardIndex={boardIndex}.");
            return;
        }

        LastHydratedTileId = "";
        Emit(TokenUpdated, "token-updated", player.playerId, player.currentTileId, false, "Token snap failed because authoritative tile was missing.");
        LogWarning($"Could not snap token for player {Display(player.playerId)} because tile {Display(player.currentTileId)} was missing.");
    }

    private MonoJoeyPlayerSnapshot SelectPlayer(MonoJoeyPlayerSnapshot[] players)
    {
        if (players == null || players.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(localPlayerId))
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && string.Equals(players[i].playerId, localPlayerId, StringComparison.Ordinal))
                {
                    return players[i];
                }
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && !players[i].isEliminated)
            {
                return players[i];
            }
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                return players[i];
            }
        }

        return null;
    }

    private HUDController.PlayerHudSnapshot ToPlayerHudSnapshot(MonoJoeyPlayerSnapshot player)
    {
        MonoJoeyLoanSnapshot loan = player.loan ?? new MonoJoeyLoanSnapshot();
        return new HUDController.PlayerHudSnapshot(
            player.playerId,
            player.username,
            player.colorId,
            player.money,
            player.currentTileId,
            loan.totalBorrowed,
            loan.currentInterestRatePercent,
            loan.nextTurnInterestDue,
            loan.loanTier.ToString(),
            player.isBankrupt,
            player.isEliminated,
            player.isLockedUp);
    }

    private static HUDController.TurnHudSnapshot ToTurnHudSnapshot(MonoJoeyTurnSnapshot turn, string fallbackPhase)
    {
        return new HUDController.TurnHudSnapshot(
            turn.currentPlayerId,
            turn.turnIndex,
            string.IsNullOrWhiteSpace(fallbackPhase) ? "unknown" : fallbackPhase,
            turn.hasRolledThisTurn,
            turn.hasResolvedTileThisTurn,
            turn.hasExecutedTileThisTurn);
    }

    private Dictionary<string, BoardTileController> BuildRuntimeTileLookup()
    {
        Dictionary<string, BoardTileController> tilesById = new Dictionary<string, BoardTileController>();
        for (int i = 0; i < boardTiles.Length; i++)
        {
            BoardTileController tile = boardTiles[i];
            if (tile != null && !string.IsNullOrWhiteSpace(tile.TileId))
            {
                tilesById[tile.TileId] = tile;
            }
        }

        return tilesById;
    }

    private static Dictionary<string, MonoJoeyBoardTileSnapshot> BuildSnapshotTileLookup(MonoJoeySnapshot snapshot)
    {
        Dictionary<string, MonoJoeyBoardTileSnapshot> tilesById = new Dictionary<string, MonoJoeyBoardTileSnapshot>();
        MonoJoeyBoardTileSnapshot[] tiles = snapshot == null || snapshot.board == null ? null : snapshot.board.tiles;
        if (tiles == null)
        {
            return tilesById;
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            MonoJoeyBoardTileSnapshot tile = tiles[i];
            if (tile != null && !string.IsNullOrWhiteSpace(tile.tileId))
            {
                tilesById[tile.tileId] = tile;
            }
        }

        return tilesById;
    }

    private static Dictionary<string, MonoJoeyPlayerSnapshot> BuildPlayerLookup(MonoJoeyPlayerSnapshot[] players)
    {
        Dictionary<string, MonoJoeyPlayerSnapshot> playersById = new Dictionary<string, MonoJoeyPlayerSnapshot>();
        if (players == null)
        {
            return playersById;
        }

        for (int i = 0; i < players.Length; i++)
        {
            MonoJoeyPlayerSnapshot player = players[i];
            if (player != null && !string.IsNullOrWhiteSpace(player.playerId))
            {
                playersById[player.playerId] = player;
            }
        }

        return playersById;
    }

    private static Color ColorForPlayer(MonoJoeyPlayerSnapshot player)
    {
        return player == null ? Color.white : ColorForId(player.colorId, player.playerId);
    }

    private static Color FallbackOwnerColor(string ownerPlayerId)
    {
        return ColorForId(ownerPlayerId, ownerPlayerId);
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

    private void ClearFreshSnapshotState()
    {
        LastSnapshot = null;
        LastHydratedSnapshotVersion = 0;
        LastHydratedSessionId = "";
        LastHydratedServerNowUtc = "";
        LastHydratedPhase = "";
        LastHydratedTurnIndex = -1;
        LastHydratedPlayerId = "";
        LastHydratedTileId = "";
        LastHydrationSucceeded = false;
        LastMovement = null;
        LastMoneyDeltas = Array.Empty<MonoJoeyMoneyDeltaPayload>();
        LastPropertyOwnershipChanges = Array.Empty<MonoJoeyPropertyOwnershipChangePayload>();
        LastPlayerEliminations = Array.Empty<MonoJoeyPlayerEliminationPayload>();
        LastHydratedHasActiveAuction = false;
        LastHydrationUtc = DateTime.MinValue;
    }

    private bool Fail(string message)
    {
        ClearFreshSnapshotState();
        Emit(HydrationFailed, "hydration-failed", "", "", false, message);
        LogWarning(message);
        return false;
    }

    private void Emit(
        Action<HydrationHookEvent> handler,
        string hookName,
        string playerId,
        string tileId,
        bool succeeded,
        string message)
    {
        if (handler == null)
        {
            return;
        }

        MonoJoeyTurnSnapshot turn = LastSnapshot == null ? null : LastSnapshot.turn;
        handler.Invoke(new HydrationHookEvent(
            hookName,
            LastSnapshot == null ? "" : LastSnapshot.sessionId,
            playerId ?? "",
            tileId ?? "",
            LastSnapshot == null ? "" : LastSnapshot.phase,
            turn == null ? -1 : turn.turnIndex,
            succeeded,
            message ?? ""));
    }

    private void Log(string message)
    {
        if (logHydration)
        {
            Debug.Log($"[SnapshotHydrator] {message} Read-only snapshot hydration; no backend mutation.", this);
        }
    }

    private void LogWarning(string message)
    {
        if (logHydration)
        {
            Debug.LogWarning($"[SnapshotHydrator] {message} Read-only snapshot hydration; no backend mutation.", this);
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
