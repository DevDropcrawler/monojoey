namespace MonoJoey.Server;

using MonoJoey.Server.GameEngine;
using MonoJoey.Server.GameEngine.Stats;
using MonoJoey.Server.Realtime;
using MonoJoey.Server.Sessions;
using MonoJoey.Server.Stats;

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
        builder.Services.AddSingleton<InMemoryStatsRepository>();
        builder.Services.AddSingleton<IStatEventSink>(services =>
            services.GetRequiredService<InMemoryStatsRepository>());
        builder.Services.AddSingleton<LeaderboardQueryService>();
        builder.Services.AddSingleton(services =>
            new LobbyMessageHandler(
                services.GetRequiredService<SessionManager>(),
                services.GetRequiredService<DiceService>(),
                services.GetRequiredService<AuctionTimerService>(),
                services.GetRequiredService<IStatEventSink>()));
        builder.Services.AddSingleton<WebSocketConnectionHandler>();

        var app = builder.Build();

        app.UseWebSockets();

        app.MapGet("/health", () => Results.Text("healthy"));

        app.MapGet(
            "/stats/leaderboards/{category}",
            (string category, int? limit, LeaderboardQueryService leaderboards) =>
            {
                if (!LeaderboardCategoryNames.TryParse(category, out var parsedCategory))
                {
                    return Results.BadRequest(new { error = "invalid_leaderboard_category" });
                }

                var normalizedLimit = LeaderboardQueryService.NormalizeLimit(
                    limit ?? LeaderboardQueryService.DefaultLimit);
                return Results.Ok(new
                {
                    category = LeaderboardCategoryNames.ToWireName(parsedCategory),
                    limit = normalizedLimit,
                    entries = leaderboards.GetLeaderboard(parsedCategory, normalizedLimit),
                });
            });

        app.MapGet(
            "/stats/players/{playerId}",
            (string playerId, LeaderboardQueryService leaderboards) =>
            {
                var stats = leaderboards.GetPlayerStats(playerId);
                return stats is null
                    ? Results.NotFound(new { error = "player_stats_not_found" })
                    : Results.Ok(stats);
            });

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
