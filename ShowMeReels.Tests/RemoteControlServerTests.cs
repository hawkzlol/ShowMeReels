using ShowMeReels.App.Services;

namespace ShowMeReels.Tests;

public sealed class RemoteControlServerTests
{
    [Theory]
    [InlineData("/up", -1)]
    [InlineData("/up?source=phone", -1)]
    [InlineData("/down", 1)]
    [InlineData("/down?source=phone", 1)]
    public void ParseDirection_MapsRemoteRoutes(string path, int expectedDirection)
    {
        Assert.Equal(expectedDirection, RemoteControlServer.ParseDirection(path));
    }

    [Fact]
    public void ParseDirection_IgnoresUnknownRoutes()
    {
        Assert.Null(RemoteControlServer.ParseDirection("/"));
        Assert.Null(RemoteControlServer.ParseDirection("/left"));
        Assert.Null(RemoteControlServer.ParseDirection("/pause"));
    }

    [Theory]
    [InlineData("/pause")]
    [InlineData("/pause?source=phone")]
    public void ParseCommand_MapsPauseRoute(string path)
    {
        Assert.Equal(RemoteControlCommand.TogglePlayPause, RemoteControlServer.ParseCommand(path));
    }

    [Fact]
    public void ParseCommand_IgnoresScrollRoutes()
    {
        Assert.Null(RemoteControlServer.ParseCommand("/up"));
        Assert.Null(RemoteControlServer.ParseCommand("/down"));
    }

    [Fact]
    public void BuildRemotePage_ContainsSplitTopControlsAndSmallDownButton()
    {
        string page = RemoteControlServer.BuildRemotePage();

        Assert.Contains("class=\"top\"", page);
        Assert.Contains("data-command=\"up\"", page);
        Assert.Contains("data-command=\"pause\"", page);
        Assert.Contains("data-command=\"down\"", page);
        Assert.Contains("class=\"down\"", page);
        Assert.Contains("grid-template-rows: 1fr minmax(84px, 22vh)", page);
        Assert.Contains("fetch(\"/\" + command", page);
        Assert.Contains("viewport-fit=cover", page);
    }
}
