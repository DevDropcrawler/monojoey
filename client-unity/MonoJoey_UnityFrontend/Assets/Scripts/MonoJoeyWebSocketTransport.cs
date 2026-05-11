using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed class MonoJoeyWebSocketTransport : MonoBehaviour, IMonoJoeyTransport
{
    [SerializeField] private int receiveBufferBytes = 8192;

    private readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellation;

    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> MessageReceived;
    public event Action<string> ErrorReceived;

    public MonoJoeyTransportConnectionState State { get; private set; } = MonoJoeyTransportConnectionState.Idle;
    public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;
    public string LastError { get; private set; } = "";

    public void Connect(string webSocketUrl)
    {
        if (State == MonoJoeyTransportConnectionState.Connecting || IsConnected)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(webSocketUrl))
        {
            PublishError("WebSocket URL is empty.");
            return;
        }

        cancellation = new CancellationTokenSource();
        webSocket = new ClientWebSocket();
        State = MonoJoeyTransportConnectionState.Connecting;
        _ = ConnectAsync(webSocketUrl, cancellation.Token);
    }

    public void Disconnect()
    {
        _ = DisconnectAsync();
    }

    public void SendJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        _ = SendJsonAsync(json);
    }

    private async Task ConnectAsync(string webSocketUrl, CancellationToken token)
    {
        try
        {
            await webSocket.ConnectAsync(new Uri(webSocketUrl), token).ConfigureAwait(false);
            EnqueueMainThread(() =>
            {
                State = MonoJoeyTransportConnectionState.ConnectedUnbound;
                Connected?.Invoke();
            });

            await ReceiveLoop(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            EnqueueMainThread(PublishDisconnected);
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => PublishError(ex.Message));
        }
    }

    private async Task ReceiveLoop(CancellationToken token)
    {
        byte[] buffer = new byte[Mathf.Max(1024, receiveBufferBytes)];
        while (!token.IsCancellationRequested && webSocket != null && webSocket.State == WebSocketState.Open)
        {
            using (MemoryStream message = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        EnqueueMainThread(PublishDisconnected);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(message.ToArray());
                    EnqueueMainThread(() => MessageReceived?.Invoke(json));
                }
            }
        }

        EnqueueMainThread(PublishDisconnected);
    }

    private async Task SendJsonAsync(string json)
    {
        try
        {
            if (!IsConnected)
            {
                PublishError("Cannot send WebSocket message while disconnected.");
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => PublishError(ex.Message));
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            cancellation?.Cancel();
            if (webSocket != null && (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived))
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unity client disconnect", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            EnqueueMainThread(() => PublishError(ex.Message));
        }
        finally
        {
            EnqueueMainThread(PublishDisconnected);
        }
    }

    private void Update()
    {
        while (mainThreadActions.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private void OnDestroy()
    {
        cancellation?.Cancel();
        webSocket?.Dispose();
        cancellation?.Dispose();
    }

    private void OnApplicationQuit()
    {
        cancellation?.Cancel();
    }

    private void EnqueueMainThread(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

    private void PublishDisconnected()
    {
        State = MonoJoeyTransportConnectionState.Disconnected;
        Disconnected?.Invoke();
    }

    private void PublishError(string message)
    {
        LastError = message ?? "";
        State = MonoJoeyTransportConnectionState.Error;
        ErrorReceived?.Invoke(LastError);
    }
}
