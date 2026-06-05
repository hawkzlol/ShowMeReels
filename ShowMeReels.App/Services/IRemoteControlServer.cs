namespace ShowMeReels.App.Services;

public interface IRemoteControlServer : IDisposable
{
    event EventHandler<RemoteControlCommandEventArgs>? CommandReceived;

    string Url { get; }

    void Start();
}
