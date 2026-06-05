using ShowMeReels.App.Services;

namespace ShowMeReels.Tests;

public sealed class ViewerToggleStateTests
{
    [Fact]
    public void Hide_TracksWhetherPlaybackShouldResume()
    {
        ViewerToggleState state = new(initiallyVisible: true);

        state.Hide(wasPlaying: true);

        Assert.False(state.IsVisible);
        Assert.True(state.WasPlayingBeforeHide);
    }

    [Fact]
    public void Show_ReturnsStoredResumeState_AndClearsIt()
    {
        ViewerToggleState state = new(initiallyVisible: false);
        state.Hide(wasPlaying: true);

        bool shouldResume = state.Show();

        Assert.True(shouldResume);
        Assert.True(state.IsVisible);
        Assert.False(state.WasPlayingBeforeHide);
    }

    [Fact]
    public void Background_TracksForegroundResumeState_WithoutHiding()
    {
        ViewerToggleState state = new(initiallyVisible: true);

        state.Background(wasPlaying: true);

        Assert.True(state.IsVisible);
        Assert.True(state.IsBackgrounded);
        Assert.True(state.WasPlayingBeforeBackground);
    }

    [Fact]
    public void BringToForeground_ReturnsStoredBackgroundResumeState_AndClearsIt()
    {
        ViewerToggleState state = new(initiallyVisible: true);
        state.Background(wasPlaying: true);

        bool shouldResume = state.BringToForeground();

        Assert.True(shouldResume);
        Assert.True(state.IsVisible);
        Assert.False(state.IsBackgrounded);
        Assert.False(state.WasPlayingBeforeBackground);
    }
}
