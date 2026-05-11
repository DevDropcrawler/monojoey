using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MonoJoeyBackendMessageRouter : MonoBehaviour
{
    private static readonly HashSet<string> SequencedGameplayBroadcasts = new HashSet<string>(StringComparer.Ordinal)
    {
        "dice_rolled",
        "tile_resolved",
        "tile_executed",
        "turn_ended",
        "auction_started",
        "bid_accepted",
        "auction_finalized",
        "loan_taken",
        "loan_interest_charged",
        "property_mortgaged",
        "property_unmortgaged",
        "property_upgraded",
        "trade_offer_created",
        "trade_offer_accepted",
        "trade_offer_declined",
        "trade_offer_cancelled",
        "player_bankrupted",
        "game_completed",
        "AuctionStarted",
        "BidAccepted",
        "AuctionWon",
        "LoanInterestCharged"
    };

    private static readonly HashSet<string> DirectGameplayCommandResults = new HashSet<string>(StringComparer.Ordinal)
    {
        MonoJoeyTransportMessageTypes.RollResult,
        MonoJoeyTransportMessageTypes.ResolveTileResult,
        MonoJoeyTransportMessageTypes.ExecuteTileResult,
        MonoJoeyTransportMessageTypes.EndTurnResult,
        MonoJoeyTransportMessageTypes.BidResult
    };

    [SerializeField] private SnapshotHydrator snapshotHydrator;
    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private bool requestSnapshotAfterSequencedBroadcast = true;

    public string LastMessageType { get; private set; } = "";
    public string LastErrorCode { get; private set; } = "";
    public string LastErrorMessage { get; private set; } = "";
    public long LastSequence { get; private set; }
    public long AdvisoryLastEventSequence { get; private set; }
    public int SnapshotResultCount { get; private set; }
    public int ReconnectResultCount { get; private set; }
    public int ErrorEnvelopeCount { get; private set; }
    public int UnknownMessageCount { get; private set; }
    public int SequencedBroadcastCount { get; private set; }
    public int DirectCommandResultCount { get; private set; }
    public int StaleSequenceCount { get; private set; }
    public int OutOfOrderSequenceCount { get; private set; }
    public string LastDirectCommandResultType { get; private set; } = "";
    public DateTime LastMessageReceivedUtc { get; private set; } = DateTime.MinValue;
    public DateTime LastSnapshotHydratedUtc { get; private set; } = DateTime.MinValue;
    public SnapshotHydrator Hydrator => snapshotHydrator;

    public void Configure(SnapshotHydrator hydrator, MonoJoeySessionClient client, bool requestSnapshotAfterBroadcast)
    {
        snapshotHydrator = hydrator;
        sessionClient = client;
        requestSnapshotAfterSequencedBroadcast = requestSnapshotAfterBroadcast;
    }

    public void RouteRawMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("[MonoJoeyBackendMessageRouter] Ignored empty backend message. Read-only transport; no backend mutation.", this);
            return;
        }

        MonoJoeyServerEnvelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<MonoJoeyServerEnvelope>(json);
        }
        catch (Exception ex)
        {
            UnknownMessageCount++;
            Debug.LogWarning($"[MonoJoeyBackendMessageRouter] Backend message parse failed: {ex.Message}. Read-only transport; no backend mutation.", this);
            return;
        }

        LastMessageType = envelope == null ? "" : envelope.type ?? "";
        LastMessageReceivedUtc = DateTime.UtcNow;
        if (LastMessageType == MonoJoeyTransportMessageTypes.SnapshotResult)
        {
            SnapshotResultCount++;
            bool hydrated = snapshotHydrator != null && snapshotHydrator.HydrateSnapshotResultJson(json);
            if (hydrated)
            {
                LastSnapshotHydratedUtc = DateTime.UtcNow;
                sessionClient?.ReportAuthoritativeHydration(MonoJoeyTransportMessageTypes.SnapshotResult);
            }

            Debug.Log($"[MonoJoeyBackendMessageRouter] snapshot_result routed to SnapshotHydrator hydrated={hydrated}. Read-only transport; no backend mutation.", this);
            return;
        }

        if (LastMessageType == MonoJoeyTransportMessageTypes.ReconnectResult)
        {
            ReconnectResultCount++;
            MonoJoeyReconnectResultEnvelope reconnect = JsonUtility.FromJson<MonoJoeyReconnectResultEnvelope>(json);
            AdvisoryLastEventSequence = reconnect == null || reconnect.payload == null ? 0 : reconnect.payload.lastEventSequence;
            LastSequence = Math.Max(LastSequence, AdvisoryLastEventSequence);
            bool hydrated = snapshotHydrator != null && snapshotHydrator.HydrateReconnectResultJson(json);
            if (hydrated)
            {
                LastSnapshotHydratedUtc = DateTime.UtcNow;
                sessionClient?.MarkBoundAfterHydration();
            }
            else
            {
                sessionClient?.ReportReconnectHydrationFailure("reconnect_result hydration failed; socket remains unbound.");
            }

            Debug.Log($"[MonoJoeyBackendMessageRouter] reconnect_result routed to SnapshotHydrator hydrated={hydrated}, advisoryLastEventSequence={AdvisoryLastEventSequence}. Read-only transport; no backend mutation.", this);
            return;
        }

        if (LastMessageType == MonoJoeyTransportMessageTypes.Error)
        {
            ErrorEnvelopeCount++;
            MonoJoeyErrorEnvelope error = JsonUtility.FromJson<MonoJoeyErrorEnvelope>(json);
            LastErrorCode = error == null || error.payload == null ? "" : error.payload.code ?? "";
            LastErrorMessage = error == null || error.payload == null ? "" : error.payload.message ?? "";
            Debug.LogWarning($"[MonoJoeyBackendMessageRouter] backend error envelope code={Display(LastErrorCode)}, message={Display(LastErrorMessage)}. Read-only transport; no local gameplay compensation.", this);
            sessionClient?.ReportBackendError(LastErrorCode, LastErrorMessage);
            return;
        }

        if (DirectGameplayCommandResults.Contains(LastMessageType))
        {
            DirectCommandResultCount++;
            LastDirectCommandResultType = LastMessageType;
            Debug.Log($"[MonoJoeyBackendMessageRouter] Direct command result {Display(LastMessageType)} observed and forwarded to dispatcher. No gameplay UI mutation; waiting for snapshot hydration.", this);
            sessionClient?.ReportDirectCommandResult(LastMessageType, json);
            return;
        }

        if (envelope != null && envelope.sequence > 0 && SequencedGameplayBroadcasts.Contains(LastMessageType))
        {
            RouteSequencedBroadcast(envelope);
            return;
        }

        UnknownMessageCount++;
        Debug.Log($"[MonoJoeyBackendMessageRouter] Unknown backend message type {Display(LastMessageType)} ignored. Read-only transport; no backend mutation.", this);
    }

    private void RouteSequencedBroadcast(MonoJoeyServerEnvelope envelope)
    {
        SequencedBroadcastCount++;
        if (envelope.sequence <= LastSequence)
        {
            StaleSequenceCount++;
            Debug.LogWarning($"[MonoJoeyBackendMessageRouter] Stale sequenced broadcast ignored: type={envelope.type}, sequence={envelope.sequence}, last={LastSequence}. Read-only transport; no incremental gameplay application.", this);
            return;
        }

        if (LastSequence > 0 && envelope.sequence != LastSequence + 1)
        {
            OutOfOrderSequenceCount++;
            Debug.LogWarning($"[MonoJoeyBackendMessageRouter] Out-of-order sequenced broadcast observed: type={envelope.type}, sequence={envelope.sequence}, expected={LastSequence + 1}. Read-only transport; no incremental gameplay application.", this);
        }

        LastSequence = envelope.sequence;
        Debug.Log($"[MonoJoeyBackendMessageRouter] Sequenced broadcast ignored for incremental UI: type={envelope.type}, sequence={envelope.sequence}. Read-only transport; no backend mutation.", this);
        if (requestSnapshotAfterSequencedBroadcast)
        {
            sessionClient?.RequestSnapshotAfterBroadcast();
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
