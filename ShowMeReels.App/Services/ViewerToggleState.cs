namespace ShowMeReels.App.Services;

public sealed class ViewerToggleState
{
    public ViewerToggleState(bool initiallyVisible)
    {
        IsVisible = initiallyVisible;
    }

    public bool IsVisible { get; private set; }

    public bool IsBackgrounded { get; private set; }

    public bool WasPlayingBeforeHide { get; private set; }

    public bool WasPlayingBeforeBackground { get; private set; }

    public void Background(bool wasPlaying)
    {
        if (!IsVisible)
        {
            return;
        }

        IsBackgrounded = true;
        WasPlayingBeforeBackground = wasPlaying;
    }

    public void Hide(bool wasPlaying)
    {
        WasPlayingBeforeHide = wasPlaying;
        WasPlayingBeforeBackground = false;
        IsBackgrounded = false;
        IsVisible = false;
    }

    public bool BringToForeground()
    {
        bool shouldResume = WasPlayingBeforeBackground;
        IsBackgrounded = false;
        WasPlayingBeforeBackground = false;
        return shouldResume;
    }

    public bool Show()
    {
        bool shouldResume = WasPlayingBeforeHide;
        IsVisible = true;
        IsBackgrounded = false;
        WasPlayingBeforeHide = false;
        WasPlayingBeforeBackground = false;
        return shouldResume;
    }
}
