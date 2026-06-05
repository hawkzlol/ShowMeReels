namespace ShowMeReels.App.Services;

public interface IGlobalArrowCaptureService : IDisposable
{
    event EventHandler<GlobalArrowPressedEventArgs>? ArrowPressed;

    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

public sealed class GlobalArrowPressedEventArgs : EventArgs
{
    public GlobalArrowPressedEventArgs(int direction)
    {
        Direction = direction < 0 ? -1 : 1;
    }

    public int Direction { get; }
}
