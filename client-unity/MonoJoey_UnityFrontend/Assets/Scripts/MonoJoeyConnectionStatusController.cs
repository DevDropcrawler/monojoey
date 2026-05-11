using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class MonoJoeyConnectionStatusController : MonoBehaviour
{
    [SerializeField] private MonoJoeySessionClient sessionClient;
    [SerializeField] private MonoJoeyBackendMessageRouter messageRouter;
    [SerializeField] private Text modeText;
    [SerializeField] private Text stateText;
    [SerializeField] private Text sessionText;
    [SerializeField] private Text lastMessageText;
    [SerializeField] private Text lastSequenceText;
    [SerializeField] private Text lastSnapshotText;
    [SerializeField] private Text lastErrorText;

    public string LastRenderedStatus { get; private set; } = "";

    public void Configure(MonoJoeySessionClient client, MonoJoeyBackendMessageRouter router)
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= Refresh;
        }

        sessionClient = client;
        messageRouter = router;
        if (sessionClient != null)
        {
            sessionClient.StatusChanged += Refresh;
        }

        Refresh();
    }

    private void OnEnable()
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (sessionClient != null)
        {
            sessionClient.StatusChanged -= Refresh;
        }
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        string mode = sessionClient == null ? "--" : sessionClient.Mode.ToString();
        string state = sessionClient == null ? "--" : sessionClient.State.ToString();
        string session = sessionClient == null ? "--" : $"{Display(sessionClient.SessionId)} / {Display(sessionClient.PlayerId)}";
        string lastType = messageRouter == null ? "--" : Display(messageRouter.LastMessageType);
        string sequence = messageRouter == null ? "--" : messageRouter.LastSequence.ToString();
        string snapshotTime = messageRouter == null || messageRouter.LastSnapshotHydratedUtc == DateTime.MinValue
            ? "--"
            : messageRouter.LastSnapshotHydratedUtc.ToString("O");
        string error = sessionClient == null ? "" : sessionClient.LastError;
        if (string.IsNullOrWhiteSpace(error) && messageRouter != null)
        {
            error = messageRouter.LastErrorMessage;
        }

        SetText(modeText, $"Mode: {mode}");
        SetText(stateText, $"State: {state}");
        SetText(sessionText, $"Session: {session}");
        SetText(lastMessageText, $"Last message: {lastType}");
        SetText(lastSequenceText, $"Last sequence: {sequence}");
        SetText(lastSnapshotText, $"Last snapshot: {snapshotTime}");
        SetText(lastErrorText, $"Last error: {Display(error)}");
        LastRenderedStatus = $"{mode}|{state}|{session}|{lastType}|{sequence}|{snapshotTime}|{Display(error)}";
    }

    private static void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }
}
