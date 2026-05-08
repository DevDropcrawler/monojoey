namespace MonoJoey.Server.Tests.Stats;

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MonoJoey.Server.GameEngine;
using MonoJoey.Server.GameEngine.Stats;
using MonoJoey.Server.Stats;
using MonoJoey.Shared.Protocol;

public class StatsEndpointTests
{
    [Fact]
    public async Task GetLeaderboard_WithValidCategoryReturnsJsonEntries()
    {
        await using var app = CreateApp(out var baseAddress);
        var repository = app.Services.GetRequiredService<InMemoryStatsRepository>();
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));

        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync("/stats/leaderboards/wins?limit=10");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("wins", json.RootElement.GetProperty("category").GetString());
        Assert.Equal(10, json.RootElement.GetProperty("limit").GetInt32());
        var entry = json.RootElement.GetProperty("entries")[0];
        Assert.Equal(1, entry.GetProperty("rank").GetInt32());
        Assert.Equal("player_1", entry.GetProperty("playerId").GetString());
        Assert.Equal(1, entry.GetProperty("value").GetInt64());
    }

    [Fact]
    public async Task GetLeaderboard_WithInvalidCategoryReturnsBadRequest()
    {
        await using var app = CreateApp(out var baseAddress);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync("/stats/leaderboards/not_a_category");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_leaderboard_category", body?.Error);
    }

    [Fact]
    public async Task GetPlayerStats_WithKnownPlayerReturnsSnapshot()
    {
        await using var app = CreateApp(out var baseAddress);
        var repository = app.Services.GetRequiredService<InMemoryStatsRepository>();
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.RentPaid, new Money(30)));

        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync("/stats/players/player_1");
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("player_1", json.RootElement.GetProperty("playerId").GetString());
        Assert.Equal(30, json.RootElement.GetProperty("rentPaid").GetInt64());
        Assert.Equal(0, json.RootElement.GetProperty("gamesWon").GetInt64());
    }

    [Fact]
    public async Task GetPlayerStats_WithUnknownPlayerReturnsNotFound()
    {
        await using var app = CreateApp(out var baseAddress);
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var response = await client.GetAsync("/stats/players/missing");
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("player_stats_not_found", body?.Error);
    }

    [Fact]
    public async Task GetEndpoints_DoNotAlterCounters()
    {
        await using var app = CreateApp(out var baseAddress);
        var repository = app.Services.GetRequiredService<InMemoryStatsRepository>();
        repository.Emit(new StatEvent(new PlayerId("player_1"), StatEventKind.GameWon));

        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = baseAddress };

        using var leaderboardResponse = await client.GetAsync("/stats/leaderboards/wins");
        using var playerResponse = await client.GetAsync("/stats/players/player_1");

        Assert.Equal(HttpStatusCode.OK, leaderboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, playerResponse.StatusCode);
        Assert.Equal(1, repository.GetPlayerStats("player_1")?.GamesWon);
    }

    private static WebApplication CreateApp(out Uri baseAddress)
    {
        var port = GetFreeTcpPort();
        baseAddress = new Uri($"http://127.0.0.1:{port}");
        return Program.BuildApp(new[] { "--urls", baseAddress.ToString() });
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record ErrorResponse(string Error);
}
