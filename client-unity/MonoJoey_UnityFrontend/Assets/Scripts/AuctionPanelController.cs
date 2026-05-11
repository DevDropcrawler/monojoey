using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class AuctionPanelController : MonoBehaviour
{
    [Serializable]
    public sealed class PlayerBidRow
    {
        [SerializeField] private string playerId = "player-1";
        [SerializeField] private Text playerLabel;
        [SerializeField] private Text bidLabel;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.22f, 0.85f);
        [SerializeField] private Color highlightedColor = new Color(0.20f, 0.55f, 0.85f, 0.95f);

        public string PlayerId => playerId;

        public void Bind(string id, int bid, bool highlighted)
        {
            playerId = id;

            if (playerLabel != null)
            {
                playerLabel.text = string.IsNullOrWhiteSpace(playerId) ? "Player" : playerId;
            }

            if (bidLabel != null)
            {
                bidLabel.text = bid > 0 ? $"${bid}" : "--";
            }

            SetHighlighted(highlighted);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
            {
                highlightImage.color = highlighted ? highlightedColor : normalColor;
            }
        }
    }

    public readonly struct BidRequest
    {
        public BidRequest(string auctionId, int amount)
        {
            AuctionId = auctionId;
            Amount = amount;
        }

        public string AuctionId { get; }
        public int Amount { get; }
    }

    [Header("Auction Snapshot")]
    [SerializeField] private string auctionId = "auction-demo";
    [SerializeField] private int currentHighBid;
    [SerializeField] private string highBidderPlayerId = "";
    [SerializeField] private string activePlayerId = "player-1";
    [SerializeField] private float remainingSeconds = 30f;
    [SerializeField] private List<PlayerBidRow> playerRows = new List<PlayerBidRow>();

    [Header("Controls")]
    [SerializeField] private InputField bidInput;
    [SerializeField] private Button bidButton;
    [SerializeField] private Text timerText;
    [SerializeField] private Text currentBidText;
    [SerializeField] private Text activePlayerText;
    [SerializeField] private Text highBidderText;
    [SerializeField] private Text logText;

    public event Action<BidRequest> BidRequested;

    public string AuctionId => auctionId;
    public int CurrentHighBid => currentHighBid;
    public string HighBidderPlayerId => highBidderPlayerId;
    public string ActivePlayerId => activePlayerId;
    public float RemainingSeconds => remainingSeconds;
    public BidRequest? LastBidRequest { get; private set; }

    private void Awake()
    {
        if (bidButton != null)
        {
            bidButton.onClick.AddListener(SubmitLocalBidRequest);
        }

        RefreshSnapshotText();
    }

    private void OnDestroy()
    {
        if (bidButton != null)
        {
            bidButton.onClick.RemoveListener(SubmitLocalBidRequest);
        }
    }

    public void BindAuctionSnapshot(
        string snapshotAuctionId,
        int snapshotCurrentHighBid,
        string snapshotHighBidderPlayerId,
        string snapshotActivePlayerId,
        float snapshotRemainingSeconds)
    {
        auctionId = snapshotAuctionId;
        currentHighBid = snapshotCurrentHighBid;
        highBidderPlayerId = snapshotHighBidderPlayerId;
        activePlayerId = snapshotActivePlayerId;
        remainingSeconds = Mathf.Max(0f, snapshotRemainingSeconds);

        RefreshSnapshotText();
    }

    public void BindPlayerBidRows(IReadOnlyList<string> playerIds, IReadOnlyList<int> bids)
    {
        int count = Mathf.Min(playerRows.Count, playerIds == null ? 0 : playerIds.Count);

        for (int i = 0; i < count; i++)
        {
            int bid = bids != null && i < bids.Count ? bids[i] : 0;
            string rowPlayerId = playerIds[i];
            playerRows[i].Bind(rowPlayerId, bid, rowPlayerId == activePlayerId || rowPlayerId == highBidderPlayerId);
        }
    }

    public void SetPlayerHighlight(string playerId, bool highlighted)
    {
        foreach (PlayerBidRow row in playerRows)
        {
            if (row.PlayerId == playerId)
            {
                row.SetHighlighted(highlighted);
                return;
            }
        }
    }

    public void AppendLog(string message)
    {
        if (logText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        logText.text = string.IsNullOrWhiteSpace(logText.text) ? message : $"{logText.text}\n{message}";
    }

    public void SubmitLocalBidRequest()
    {
        if (bidInput == null)
        {
            AppendLog("Bid request ignored: bid input is not wired.");
            return;
        }

        if (!int.TryParse(bidInput.text, out int amount) || amount <= 0)
        {
            AppendLog($"Invalid local bid: {bidInput.text}");
            return;
        }

        LastBidRequest = new BidRequest(auctionId, amount);
        AppendLog($"Local bid request: {auctionId} ${amount}");
        BidRequested?.Invoke(LastBidRequest.Value);
    }

    private void RefreshSnapshotText()
    {
        if (timerText != null)
        {
            timerText.text = $"{Mathf.CeilToInt(remainingSeconds)}s";
        }

        if (currentBidText != null)
        {
            currentBidText.text = $"Current Bid: ${currentHighBid}";
        }

        if (activePlayerText != null)
        {
            activePlayerText.text = $"Active: {DisplayPlayerId(activePlayerId)}";
        }

        if (highBidderText != null)
        {
            highBidderText.text = $"High Bidder: {DisplayPlayerId(highBidderPlayerId)}";
        }
    }

    private static string DisplayPlayerId(string playerId)
    {
        return string.IsNullOrWhiteSpace(playerId) ? "--" : playerId;
    }
}
