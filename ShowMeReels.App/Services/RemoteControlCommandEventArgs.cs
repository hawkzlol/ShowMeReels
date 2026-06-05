namespace ShowMeReels.App.Services;

public enum RemoteControlCommand
{
    Scroll,
    TogglePlayPause,
}

public sealed class RemoteControlCommandEventArgs : EventArgs
{
    public RemoteControlCommandEventArgs(int direction)
    {
        Command = RemoteControlCommand.Scroll;
        Direction = direction < 0 ? -1 : 1;
    }

    public RemoteControlCommandEventArgs(RemoteControlCommand command)
    {
        Command = command;
        Direction = 0;
    }

    public RemoteControlCommand Command { get; }

    public int Direction { get; }
}
