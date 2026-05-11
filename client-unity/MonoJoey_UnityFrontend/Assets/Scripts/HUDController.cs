using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class HUDController : MonoBehaviour
{
    public readonly struct PlayerHudSnapshot
    {
        public PlayerHudSnapshot(
            string playerId,
            string username,
            string colorId,
            int money,
            string currentTileId,
            int loanTotalBorrowed,
            float loanInterestRatePercent,
            int loanNextTurnInterestDue,
            string loanTier,
            bool isBankrupt,
            bool isEliminated,
            bool isLockedUp)
        {
            PlayerId = playerId;
            Username = username;
            ColorId = colorId;
            Money = money;
            CurrentTileId = currentTileId;
            LoanTotalBorrowed = loanTotalBorrowed;
            LoanInterestRatePercent = loanInterestRatePercent;
            LoanNextTurnInterestDue = loanNextTurnInterestDue;
            LoanTier = loanTier;
            IsBankrupt = isBankrupt;
            IsEliminated = isEliminated;
            IsLockedUp = isLockedUp;
        }

        public string PlayerId { get; }
        public string Username { get; }
        public string ColorId { get; }
        public int Money { get; }
        public string CurrentTileId { get; }
        public int LoanTotalBorrowed { get; }
        public float LoanInterestRatePercent { get; }
        public int LoanNextTurnInterestDue { get; }
        public string LoanTier { get; }
        public bool IsBankrupt { get; }
        public bool IsEliminated { get; }
        public bool IsLockedUp { get; }
    }

    public readonly struct TurnHudSnapshot
    {
        public TurnHudSnapshot(
            string currentPlayerId,
            int turnIndex,
            string phase,
            bool hasRolledThisTurn,
            bool hasResolvedTileThisTurn,
            bool hasExecutedTileThisTurn)
        {
            CurrentPlayerId = currentPlayerId;
            TurnIndex = turnIndex;
            Phase = phase;
            HasRolledThisTurn = hasRolledThisTurn;
            HasResolvedTileThisTurn = hasResolvedTileThisTurn;
            HasExecutedTileThisTurn = hasExecutedTileThisTurn;
        }

        public string CurrentPlayerId { get; }
        public int TurnIndex { get; }
        public string Phase { get; }
        public bool HasRolledThisTurn { get; }
        public bool HasResolvedTileThisTurn { get; }
        public bool HasExecutedTileThisTurn { get; }
    }

    [Header("Fields")]
    [SerializeField] private Text currentPlayerText;
    [SerializeField] private Text turnStatusText;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text loanText;
    [SerializeField] private Text tileText;
    [SerializeField] private Image turnIndicatorImage;
    [SerializeField] private Text debugLogText;
    [SerializeField] private Color activeTurnColor = new Color(0.25f, 0.70f, 0.42f, 1f);
    [SerializeField] private Color inactiveTurnColor = new Color(0.35f, 0.38f, 0.42f, 1f);
    [SerializeField] private bool showDebugLog = true;

    public PlayerHudSnapshot LastPlayerSnapshot { get; private set; }
    public TurnHudSnapshot LastTurnSnapshot { get; private set; }

    private void Awake()
    {
        if (debugLogText != null)
        {
            debugLogText.gameObject.SetActive(showDebugLog);
        }
    }

    public void BindSnapshot(PlayerHudSnapshot player, TurnHudSnapshot turn)
    {
        LastPlayerSnapshot = player;
        LastTurnSnapshot = turn;

        bool isCurrentTurn = string.Equals(player.PlayerId, turn.CurrentPlayerId, StringComparison.Ordinal);

        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"{DisplayText(player.Username, player.PlayerId)} ({DisplayText(player.ColorId, "no-color")})";
        }

        if (turnStatusText != null)
        {
            turnStatusText.text = $"Turn {turn.TurnIndex}: {DisplayText(turn.Phase, "Unknown")} | Rolled: {YesNo(turn.HasRolledThisTurn)} | Tile: {YesNo(turn.HasResolvedTileThisTurn)} | Action: {YesNo(turn.HasExecutedTileThisTurn)}";
        }

        if (moneyText != null)
        {
            moneyText.text = $"Money: ${player.Money}";
        }

        if (loanText != null)
        {
            loanText.text = $"Loans: ${player.LoanTotalBorrowed} | {player.LoanInterestRatePercent:0.#}% | Next: ${player.LoanNextTurnInterestDue} | {DisplayText(player.LoanTier, "none")}";
        }

        if (tileText != null)
        {
            string status = PlayerStatus(player);
            tileText.text = string.IsNullOrWhiteSpace(status)
                ? $"Tile: {DisplayText(player.CurrentTileId, "--")}"
                : $"Tile: {DisplayText(player.CurrentTileId, "--")} | {status}";
        }

        if (turnIndicatorImage != null)
        {
            turnIndicatorImage.color = isCurrentTurn ? activeTurnColor : inactiveTurnColor;
        }

        AppendDebugLog($"HUD bound player={player.PlayerId}, money={player.Money}, loan={player.LoanTotalBorrowed}, turn={turn.TurnIndex}/{turn.Phase}.");
    }

    public void AppendDebugLog(string message)
    {
        if (!showDebugLog || debugLogText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        debugLogText.gameObject.SetActive(true);
        debugLogText.text = string.IsNullOrWhiteSpace(debugLogText.text) ? message : $"{debugLogText.text}\n{message}";
    }

    public void ClearDebugLog()
    {
        if (debugLogText != null)
        {
            debugLogText.text = "";
        }
    }

    private static string PlayerStatus(PlayerHudSnapshot player)
    {
        if (player.IsEliminated)
        {
            return "Eliminated";
        }

        if (player.IsBankrupt)
        {
            return "Bankrupt";
        }

        return player.IsLockedUp ? "Locked up" : "";
    }

    private static string DisplayText(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }
}
