namespace MonoJoey.Server.Sessions;

using MonoJoey.Shared.Protocol;

public sealed record PlayerConnection(
    PlayerId PlayerId,
    string ConnectionId,
    bool IsReady);
