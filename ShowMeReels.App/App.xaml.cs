using System.IO;
using ShowMeReels.App.Models;
using ShowMeReels.App.Services;

namespace ShowMeReels.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        AppDiagnostics.Log("App startup begin.");

        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShowMeReels");
        string settingsPath = Path.Combine(appDataDirectory, "settings.json");
        AppDiagnostics.Log($"Loading settings from {settingsPath}.");
        ISettingsStore settingsStore = new JsonSettingsStore(settingsPath);
        AppSettings settings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        AppDiagnostics.Log("Settings loaded.");

        AppDiagnostics.Log("Constructing MainWindow.");
        MainWindow = new MainWindow(
            settings,
            settingsStore,
            new WindowPlacementService(),
            new GlobalHotkeyService(),
            new GlobalArrowCaptureService(),
            new RemoteControlServer(),
            new WebViewScriptController());
        AppDiagnostics.Log("MainWindow constructed.");
        MainWindow.Show();
        AppDiagnostics.Log("MainWindow.Show() called.");
        if (MainWindow is MainWindow mainWindowInstance)
        {
            _ = mainWindowInstance.RevealAsync();
            AppDiagnostics.Log("Initial RevealAsync() requested.");
        }
    }
}
