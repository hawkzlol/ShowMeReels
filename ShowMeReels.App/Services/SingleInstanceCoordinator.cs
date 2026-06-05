using System.Threading;

namespace ShowMeReels.App.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string ActivateEventName = @"Local\ShowMeReels.Activate";
    private const string MutexName = @"Local\ShowMeReels.Singleton";

    private readonly EventWaitHandle _activateEvent;
    private readonly EventWaitHandle _shutdownEvent;
    private readonly Mutex _mutex;
    private bool _isPrimary;
    private Task? _listenerTask;

    public SingleInstanceCoordinator()
    {
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
        _mutex = new Mutex(initiallyOwned: false, MutexName, out bool createdNew);

        if (createdNew)
        {
            try
            {
                _isPrimary = _mutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException)
            {
                _isPrimary = true;
            }
        }
    }

    public void Dispose()
    {
        _shutdownEvent.Set();

        if (_listenerTask is not null)
        {
            _listenerTask.Wait(TimeSpan.FromSeconds(1));
        }

        if (_isPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activateEvent.Dispose();
        _shutdownEvent.Dispose();
    }

    public void SignalPrimary()
    {
        _activateEvent.Set();
    }

    public void StartListening(Action onActivation)
    {
        if (!_isPrimary || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(() =>
        {
            WaitHandle[] handles = [_activateEvent, _shutdownEvent];

            while (true)
            {
                int signaledIndex = WaitHandle.WaitAny(handles);
                if (signaledIndex == 1)
                {
                    return;
                }

                onActivation();
            }
        });
    }

    public bool TryAcquirePrimary()
    {
        return _isPrimary;
    }
}
