using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LiveSessionContext : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerDisplayRow
    {
        public string playerId = "";
        public string username = "";
        public string tokenId = "";
        public string colorId = "";
        public string currentTileId = "";
        public int money;
        public bool isLocalPlayer;
        public bool isCurrentTurnPlayer;
        public bool isBankrupt;
        public bool isEliminated;
        public bool isLockedUp;

        public string DisplayText
        {
            get
            {
                string markers = "";
                if (isLocalPlayer)
                {
                    markers += "[local]";
                }

                if (isCurrentTurnPlayer)
                {
                    markers += string.IsNullOrEmpty(markers) ? "[turn]" : " [turn]";
                }

                string flags = "";
                if (isBankrupt)
                {
                    flags += " bankrupt";
                }

                if (isEliminated)
                {
                    flags += " eliminated";
                }

                if (isLockedUp)
                {
                    flags += " locked";
                }

                return $"{Display(playerId)} {markers} name={Display(username)} money={money} tile={Display(currentTileId)} token={Display(tokenId)} color={Display(colorId)}{flags}";
            }
        }
    }

    [SerializeField] private string backendUrl = "";
    [SerializeField] private string sessionId = "";
    [SerializeField] private string playerId = "";
    [SerializeField] private MonoJoeySessionClientMode mode = MonoJoeySessionClientMode.MockValidation;
    [SerializeField] private MonoJoeyTransportConnectionState connectionState = MonoJoeyTransportConnectionState.Idle;
    [SerializeField] private bool isTransportConnected;
    [SerializeField] private bool isBoundToIdentity;
    [SerializeField] private int snapshotVersion;
    [SerializeField] private string snapshotSessionId = "";
    [SerializeField] private string snapshotStatus = "";
    [SerializeField] private string snapshotGameStatus = "";
    [SerializeField] private string snapshotPhase = "";
    [SerializeField] private int turnIndex = -1;
    [SerializeField] private string currentTurnPlayerId = "";
    [SerializeField] private string selectedPlayerId = "";
    [SerializeField] private string latestSnapshotIdentity = "";
    [SerializeField] private List<PlayerDisplayRow> players = new List<PlayerDisplayRow>();

    public string BackendUrl => backendUrl;
    public string SessionId => sessionId;
    public string PlayerId => playerId;
    public MonoJoeySessionClientMode Mode => mode;
    public MonoJoeyTransportConnectionState ConnectionState => connectionState;
    public bool IsTransportConnected => isTransportConnected;
    public bool IsBoundToIdentity => isBoundToIdentity;
    public int SnapshotVersion => snapshotVersion;
    public string SnapshotSessionId => snapshotSessionId;
    public string SnapshotStatus => snapshotStatus;
    public string SnapshotGameStatus => snapshotGameStatus;
    public string SnapshotPhase => snapshotPhase;
    public int TurnIndex => turnIndex;
    public string CurrentTurnPlayerId => currentTurnPlayerId;
    public string SelectedPlayerId => selectedPlayerId;
    public string LatestSnapshotIdentity => latestSnapshotIdentity;
    public IReadOnlyList<PlayerDisplayRow> Players => players;

    public string ConnectionSummary { get; private set; } = "";
    public string SnapshotSummary { get; private set; } = "";
    public string PlayerListSummary { get; private set; } = "";

    public void RefreshFrom(
        MonoJoeySessionClient sessionClient,
        SnapshotHydrator snapshotHydrator,
        MonoJoeySessionClientMode selectedMode,
        string formBackendUrl,
        string formSessionId,
        string formPlayerId)
    {
        mode = sessionClient == null ? selectedMode : sessionClient.Mode;
        backendUrl = sessionClient == null ? formBackendUrl ?? "" : sessionClient.WebSocketUrl;
        sessionId = sessionClient == null ? formSessionId ?? "" : sessionClient.SessionId;
        playerId = sessionClient == null ? formPlayerId ?? "" : sessionClient.PlayerId;
        connectionState = sessionClient == null ? MonoJoeyTransportConnectionState.Idle : sessionClient.State;
        isTransportConnected = sessionClient != null && sessionClient.IsTransportConnected;
        isBoundToIdentity = sessionClient != null && sessionClient.IsBoundToIdentity;

        MonoJoeySnapshot snapshot = snapshotHydrator == null ? null : snapshotHydrator.LastSnapshot;
        RefreshSnapshot(snapshot, playerId);
        BuildSummaries();
    }

    public void RefreshSnapshot(MonoJoeySnapshot snapshot, string localPlayerId)
    {
        players.Clear();
        selectedPlayerId = localPlayerId ?? "";

        if (snapshot == null)
        {
            snapshotVersion = 0;
            snapshotSessionId = "";
            snapshotStatus = "";
            snapshotGameStatus = "";
            snapshotPhase = "";
            turnIndex = -1;
            currentTurnPlayerId = "";
            latestSnapshotIdentity = "";
            return;
        }

        snapshotVersion = snapshot.snapshotVersion;
        snapshotSessionId = snapshot.sessionId ?? "";
        snapshotStatus = snapshot.status ?? "";
        snapshotGameStatus = snapshot.gameStatus ?? "";
        snapshotPhase = snapshot.phase ?? "";
        MonoJoeyTurnSnapshot turn = snapshot.turn;
        turnIndex = turn == null ? -1 : turn.turnIndex;
        currentTurnPlayerId = turn == null ? "" : turn.currentPlayerId ?? "";
        latestSnapshotIdentity = $"v={snapshotVersion} session={Display(snapshotSessionId)} phase={Display(snapshotPhase)} turn={turnIndex}";

        MonoJoeyPlayerSnapshot[] snapshotPlayers = snapshot.players ?? Array.Empty<MonoJoeyPlayerSnapshot>();
        for (int i = 0; i < snapshotPlayers.Length; i++)
        {
            MonoJoeyPlayerSnapshot player = snapshotPlayers[i];
            if (player == null)
            {
                continue;
            }

            players.Add(new PlayerDisplayRow
            {
                playerId = player.playerId ?? "",
                username = player.username ?? "",
                tokenId = player.tokenId ?? "",
                colorId = player.colorId ?? "",
                currentTileId = player.currentTileId ?? "",
                money = player.money,
                isLocalPlayer = string.Equals(player.playerId, selectedPlayerId, StringComparison.Ordinal),
                isCurrentTurnPlayer = string.Equals(player.playerId, currentTurnPlayerId, StringComparison.Ordinal),
                isBankrupt = player.isBankrupt,
                isEliminated = player.isEliminated,
                isLockedUp = player.isLockedUp
            });
        }
    }

    private void BuildSummaries()
    {
        string connected = isTransportConnected ? "connected" : "disconnected";
        string bound = isBoundToIdentity ? "bound" : "unbound";
        ConnectionSummary = $"mode={mode}|state={connectionState}|{connected}|{bound}|session={Display(sessionId)}|player={Display(playerId)}|url={Display(backendUrl)}";
        SnapshotSummary = string.IsNullOrWhiteSpace(latestSnapshotIdentity)
            ? "snapshot=--"
            : $"{latestSnapshotIdentity} status={Display(snapshotStatus)} game={Display(snapshotGameStatus)} current={Display(currentTurnPlayerId)} local={Display(selectedPlayerId)}";

        if (players.Count == 0)
        {
            PlayerListSummary = "--";
            return;
        }

        List<string> rows = new List<string>(players.Count);
        for (int i = 0; i < players.Count; i++)
        {
            rows.Add(players[i].DisplayText);
        }

        PlayerListSummary = string.Join("\n", rows);
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
