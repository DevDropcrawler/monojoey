namespace MonoJoey.Server.Tests;

using MonoJoey.Shared.Dtos;
using MonoJoey.Shared.Protocol;

public class BaselineTests
{
    [Fact]
    public void TestProjectRuns()
    {
        Assert.True(true);
    }

    [Fact]
    public void SharedContractsAreReferenceable()
    {
        var profile = new PlayerProfileSelectionDto("Josh", "token_car_placeholder", "gold");
        var matchId = new MatchId("match_123");

        Assert.Equal("Josh", profile.Username);
        Assert.Equal("match_123", matchId.Value);
        Assert.Equal(ClientMessageType.CreateLobbyRequest, ClientMessageType.CreateLobbyRequest);
    }
}
