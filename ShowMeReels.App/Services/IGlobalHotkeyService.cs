using System.Windows;

namespace ShowMeReels.App.Services;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;

    void Register(Window window);

    void Unregister();
}
