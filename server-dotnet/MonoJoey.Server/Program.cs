namespace MonoJoey.Server;

using MonoJoey.Server.GameEngine;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var app = BuildApp(args);
        app.Run();
    }

    public static WebApplication BuildApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
        builder.Services.AddSingleton<IDiceRoller, RandomDiceRoller>();
        builder.Services.AddSingleton<DiceService>();
        builder.Services.AddSingleton<SessionManager>();
        builder.Services.AddSingleton<AuctionTimerService>();
        builder.Services.AddSingleton<LobbyMessageHandler>();
        builder.Services.AddSingleton<WebSocketConnectionHandler>();

        var app = builder.Build();

        app.UseWebSockets();

        app.MapGet("/health", () => Results.Text("healthy"));

        app.Map(
            "/ws",
            async (HttpContext context, WebSocketConnectionHandler connectionHandler) =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await connectionHandler.HandleAsync(webSocket, context.RequestAborted);
            });

        return app;
    }
}

public sealed class ServerAssemblyMarker;
