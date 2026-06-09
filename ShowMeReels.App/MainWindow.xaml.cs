using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using ShowMeReels.App.Models;
using ShowMeReels.App.Services;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ShowMeReels.App;

public partial class MainWindow : Window
{
    private static readonly TimeSpan AutoHideSuppressionDuration = TimeSpan.FromMilliseconds(400);
    private readonly AppSettings _settings;
    private readonly ISettingsStore _settingsStore;
    private readonly IWindowPlacementService _placementService;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly IGlobalArrowCaptureService _globalArrowCaptureService;
    private readonly IRemoteControlServer _remoteControlServer;
    private readonly IWebViewScriptController _webViewScriptController;
    private readonly ViewerToggleState _viewerToggleState = new(initiallyVisible: true);
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _toggleMenuItem;
    private readonly Drawing.Icon _trayIcon;
    private DateTimeOffset _autoHideSuppressedUntil = DateTimeOffset.MinValue;
    private bool _isClosing;
    private bool _isHidingToTray;
    private bool _isInitializingWindow = true;
    private bool _isPinnedArrowCaptureEnabled;
    private bool _isPinnedOnTop;
    private bool _isRestoringFromBackground;
    private bool _isStartupRevealPending = true;
    private bool _isTaskbarMinimizeInProgress;
    private bool _isWebViewReady;
    private bool _isWebViewSuspended;

    public MainWindow(
        AppSettings settings,
        ISettingsStore settingsStore,
        IWindowPlacementService placementService,
        IGlobalHotkeyService hotkeyService,
        IGlobalArrowCaptureService globalArrowCaptureService,
        IRemoteControlServer remoteControlServer,
        IWebViewScriptController webViewScriptController)
    {
        _settings = settings.Normalize();
        _settings.LastUrl = AppSettings.GetDefaultUrl(_settings.Platform);
        _settingsStore = settingsStore;
        _placementService = placementService;
        _hotkeyService = hotkeyService;
        _globalArrowCaptureService = globalArrowCaptureService;
        _remoteControlServer = remoteControlServer;
        _webViewScriptController = webViewScriptController;

        InitializeComponent();

        _settingsSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _settingsSaveTimer.Tick += SettingsSaveTimer_Tick;

        _toggleMenuItem = new Forms.ToolStripMenuItem("Hide", null, ToggleMenuItem_Click);
        _trayIcon = LoadAppIcon();
        _notifyIcon = CreateNotifyIcon();

        ConfigureInitialWindowBounds();
        ApplySettingsToControls();
        UpdateArrowCaptureAvailability();
        UpdateTrayMenuText();
        AppDiagnostics.Log("MainWindow initialized controls.");
        TryRegisterGlobalHotkey();
        _globalArrowCaptureService.ArrowPressed += GlobalArrowCaptureService_ArrowPressed;
        _remoteControlServer.CommandReceived += RemoteControlServer_CommandReceived;
        TryStartRemoteControlServer();

        Loaded += MainWindow_Loaded;
        LocationChanged += MainWindow_LocationChanged;
        SizeChanged += MainWindow_SizeChanged;
        StateChanged += MainWindow_StateChanged;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        ContentRendered += MainWindow_ContentRendered;
        Closing += MainWindow_Closing;

        _isInitializingWindow = false;
    }

    private void ApplySettingsToControls()
    {
        PlaybackSpeedSlider.Value = _settings.PlaybackSpeed;
        VolumeSlider.Value = _settings.VolumePercent;
        SeekBarCheckBox.IsChecked = _settings.SeekBarEnabled;
        HardwareAccelerationCheckBox.IsChecked = _settings.HardwareAccelerationEnabled;
        SkipSeenReelsCheckBox.IsChecked = _settings.SkipSeenReelsEnabled;
        BackgroundPlaybackCheckBox.IsChecked = _settings.AllowBackgroundPlayback;
        PlaybackSpeedValueText.Text = $"{_settings.PlaybackSpeed:0.##}x";
        VolumeValueText.Text = $"{_settings.VolumePercent}%";
        ApplyPlatformSelection();
    }

    private async Task ApplyCurrentSettingsAsync()
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return;
        }

        ResumeWebViewIfNeeded();
        await coreWebView.ExecuteScriptAsync(_webViewScriptController.BuildApplySettingsScript(_settings));
    }

    private async Task ExecuteWebViewCommandAsync(string script)
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return;
        }

        ResumeWebViewIfNeeded();
        await coreWebView.ExecuteScriptAsync(script);
    }

    private async Task<bool> ExecuteWebViewCommandWithBooleanResultAsync(string script)
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return false;
        }

        ResumeWebViewIfNeeded();
        string result = await coreWebView.ExecuteScriptAsync(script);
        return _webViewScriptController.ParseBooleanResult(result);
    }

    private async Task SetHostActiveAsync(bool isActive)
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return;
        }

        if (isActive)
        {
            ResumeWebViewIfNeeded();
        }
        else if (_isWebViewSuspended || coreWebView.IsSuspended)
        {
            return;
        }

        await coreWebView.ExecuteScriptAsync(_webViewScriptController.BuildSetHostActiveScript(isActive));
    }

    private string BuildBrowserArguments()
    {
        if (_settings.HardwareAccelerationEnabled)
        {
            return "--autoplay-policy=no-user-gesture-required";
        }

        return "--autoplay-policy=no-user-gesture-required --disable-gpu --disable-gpu-compositing";
    }

    private void CaptureCurrentBounds()
    {
        if (!_viewerToggleState.IsVisible || WindowState != WindowState.Normal)
        {
            return;
        }

        _settings.WindowBounds = new WindowBounds
        {
            Left = Left,
            Top = Top,
            Width = ActualWidth > 0 ? ActualWidth : Width,
            Height = ActualHeight > 0 ? ActualHeight : Height,
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ConfigureInitialWindowBounds()
    {
        Rect workArea = SystemParameters.WorkArea;
        WindowBounds bounds = _placementService.ResolveStartupBounds(workArea, _settings.WindowBounds);

        _settings.WindowBounds = bounds;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        Forms.ContextMenuStrip contextMenu = new();
        contextMenu.Items.Add(_toggleMenuItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, ExitMenuItem_Click));

        Forms.NotifyIcon notifyIcon = new()
        {
            ContextMenuStrip = contextMenu,
            Icon = _trayIcon,
            Text = "ShowMeReels",
            Visible = true,
        };
        notifyIcon.MouseClick += NotifyIcon_MouseClick;
        return notifyIcon;
    }

    private Uri CreateStartUri()
    {
        return new Uri(AppSettings.GetDefaultUrl(_settings.Platform));
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(Close));
    }

    private void CloseSettingsPopup()
    {
        SettingsPopup.IsOpen = false;
        SettingsButton.IsChecked = false;
    }

    private static T? FindVisualParent<T>(DependencyObject? dependencyObject)
        where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T typedValue)
            {
                return typedValue;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }

    private void FocusWebView()
    {
        if (!_viewerToggleState.IsVisible)
        {
            return;
        }

        ReelsWebView.Focus();
        Keyboard.Focus(ReelsWebView);
    }

    private CoreWebView2? GetCoreWebView()
    {
        if (!_isWebViewReady)
        {
            return null;
        }

        return ReelsWebView.CoreWebView2;
    }

    private void ResumeWebViewIfNeeded()
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null || (!_isWebViewSuspended && !coreWebView.IsSuspended))
        {
            return;
        }

        try
        {
            coreWebView.Resume();
            _isWebViewSuspended = false;
            SetWebViewMemoryUsageTarget(isLowPriority: false);
            AppDiagnostics.Log("WebView resumed.");
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log($"WebView resume failed: {exception.Message}");
        }
    }

    private void SetWebViewVisibility(bool isVisible)
    {
        Visibility targetVisibility = isVisible ? Visibility.Visible : Visibility.Hidden;
        if (ReelsWebView.Visibility == targetVisibility)
        {
            return;
        }

        ReelsWebView.Visibility = targetVisibility;
    }

    private void SetWebViewMemoryUsageTarget(bool isLowPriority)
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null || _isWebViewSuspended || coreWebView.IsSuspended)
        {
            return;
        }

        try
        {
            coreWebView.MemoryUsageTargetLevel = isLowPriority
                ? CoreWebView2MemoryUsageTargetLevel.Low
                : CoreWebView2MemoryUsageTargetLevel.Normal;
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log($"WebView memory target update failed: {exception.Message}");
        }
    }

    private async Task SuspendWebViewIfPossibleAsync()
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return;
        }

        if (_isWebViewSuspended || coreWebView.IsSuspended)
        {
            _isWebViewSuspended = true;
            return;
        }

        if (ReelsWebView.IsVisible)
        {
            SetWebViewMemoryUsageTarget(isLowPriority: true);
            AppDiagnostics.Log("Skipping WebView suspension because the control is still visible.");
            return;
        }

        try
        {
            bool wasSuspended = await coreWebView.TrySuspendAsync();
            _isWebViewSuspended = wasSuspended || coreWebView.IsSuspended;
            AppDiagnostics.Log(_isWebViewSuspended
                ? "WebView suspended."
                : "WebView suspension request was declined.");
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log($"WebView suspension failed: {exception.Message}");
        }
    }

    private void SetBackgroundEfficiencyMode(bool enable)
    {
        try
        {
            ProcessPowerThrottlingState throttlingState = new()
            {
                Version = ProcessPowerThrottlingCurrentVersion,
                ControlMask = ProcessPowerThrottlingExecutionSpeed,
                StateMask = enable ? ProcessPowerThrottlingExecutionSpeed : 0,
            };

            SetProcessInformation(
                Process.GetCurrentProcess().Handle,
                ProcessInformationClass.ProcessPowerThrottling,
                ref throttlingState,
                (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
        }
        catch
        {
        }
    }

    private async Task BackgroundWindowAsync()
    {
        if (!_viewerToggleState.IsVisible || _isHidingToTray)
        {
            return;
        }

        AppDiagnostics.Log("BackgroundWindowAsync invoked.");
        bool wasPlaying = await PauseAndMuteAsync();
        await SetHostActiveAsync(isActive: false);
        SetWebViewMemoryUsageTarget(isLowPriority: true);
        SetBackgroundEfficiencyMode(enable: true);
        _viewerToggleState.Background(wasPlaying);

        SuppressAutoHideBriefly();
        CloseSettingsPopup();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ShowInTaskbar = true;
        Show();
        SendWindowToBack();
        UpdateArrowCaptureAvailability();
        UpdateTrayMenuText();
    }

    private void ApplyPinnedTopmostState()
    {
        Topmost = _isPinnedOnTop;
        PinButton.IsChecked = _isPinnedOnTop;
        UpdateArrowCaptureAvailability();
    }

    private void ApplyPlatformSelection()
    {
        bool isTikTok = _settings.Platform == ContentPlatform.TikTok;
        InstagramPlatformButton.IsChecked = !isTikTok;
        TikTokPlatformButton.IsChecked = isTikTok;
        SkipSeenReelsCheckBox.IsEnabled = !isTikTok;
        SkipSeenReelsCheckBox.Opacity = isTikTok ? 0.58 : 1.0;
        SkipSeenReelsCheckBox.ToolTip = isTikTok
            ? "Disabled on TikTok."
            : "Automatically skip videos already seen this session.";
    }

    private void BringWindowToFront()
    {
        Activate();

        if (_isPinnedOnTop)
        {
            Topmost = true;
        }
        else
        {
            Topmost = true;
            Topmost = false;
        }

        Focus();
        FocusWebView();
    }

    private void UpdateArrowCaptureAvailability()
    {
        bool canCaptureArrows = _isPinnedOnTop
            && _viewerToggleState.IsVisible
            && !_isClosing
            && WindowState != WindowState.Minimized;

        if (!canCaptureArrows && _isPinnedArrowCaptureEnabled)
        {
            _isPinnedArrowCaptureEnabled = false;
        }

        ArrowCaptureButton.IsChecked = _isPinnedArrowCaptureEnabled;
        ArrowCaptureButton.IsEnabled = canCaptureArrows;
        ArrowCaptureButton.Opacity = canCaptureArrows ? 1.0 : 0.5;
        _globalArrowCaptureService.SetEnabled(canCaptureArrows && _isPinnedArrowCaptureEnabled);
    }

    private async Task HideToTrayAsync()
    {
        if (!_viewerToggleState.IsVisible || _isHidingToTray)
        {
            return;
        }

        _isHidingToTray = true;

        try
        {
            AppDiagnostics.Log("HideToTrayAsync invoked.");
            bool wasPlaying = await PauseAndMuteAsync();
            await SetHostActiveAsync(isActive: false);
            CaptureCurrentBounds();
            _viewerToggleState.Hide(wasPlaying);

            CloseSettingsPopup();
            SetWebViewVisibility(isVisible: false);
            ShowInTaskbar = false;
            Hide();
            await SuspendWebViewIfPossibleAsync();
            SetBackgroundEfficiencyMode(enable: true);
            UpdateArrowCaptureAvailability();
            UpdateTrayMenuText();
            ScheduleSettingsSave();
        }
        finally
        {
            _isHidingToTray = false;
        }
    }

    private async void HotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        await ToggleVisibilityAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        if (_isWebViewReady)
        {
            return;
        }

        try
        {
            string webViewDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShowMeReels",
                "WebView2");
            CoreWebView2EnvironmentOptions browserOptions = new()
            {
                AdditionalBrowserArguments = BuildBrowserArguments(),
            };
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: webViewDirectory,
                options: browserOptions);

            await ReelsWebView.EnsureCoreWebView2Async(environment);
            await ReelsWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(_webViewScriptController.BuildBootstrapScript());
            ReelsWebView.CoreWebView2.WebMessageReceived += ReelsWebView_WebMessageReceived;

            ReelsWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            ReelsWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            ReelsWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            _isWebViewReady = true;
            _isWebViewSuspended = false;
            ReelsWebView.Source = CreateStartUri();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"WebView2 failed to initialize.{Environment.NewLine}{exception.Message}",
                "ShowMeReels",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _settingsSaveTimer.Stop();
        CaptureCurrentBounds();
        _settingsStore.SaveAsync(_settings.Normalize()).GetAwaiter().GetResult();

        _hotkeyService.HotkeyPressed -= HotkeyService_HotkeyPressed;
        _hotkeyService.Unregister();
        _hotkeyService.Dispose();
        _globalArrowCaptureService.ArrowPressed -= GlobalArrowCaptureService_ArrowPressed;
        _globalArrowCaptureService.Dispose();
        _remoteControlServer.CommandReceived -= RemoteControlServer_CommandReceived;
        _remoteControlServer.Dispose();

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        CaptureCurrentBounds();
        ScheduleSettingsSave();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppDiagnostics.Log("MainWindow loaded.");
        await InitializeWebViewAsync();
        SetWebViewVisibility(isVisible: true);
        await SetHostActiveAsync(isActive: true);
        SetWebViewMemoryUsageTarget(isLowPriority: false);
        SetBackgroundEfficiencyMode(enable: false);
        FocusWebView();
    }

    private async void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_isClosing
            || _isInitializingWindow
            || _isStartupRevealPending
            || _isHidingToTray
            || !_viewerToggleState.IsVisible
            || _viewerToggleState.IsBackgrounded
            || !IsLoaded
            || !IsVisible
            || DateTimeOffset.UtcNow < _autoHideSuppressedUntil)
        {
            return;
        }

        if (_settings.AllowBackgroundPlayback)
        {
            AppDiagnostics.Log("MainWindow deactivated; background playback is enabled.");
            SetWebViewMemoryUsageTarget(isLowPriority: false);
            SetBackgroundEfficiencyMode(enable: false);
            return;
        }

        AppDiagnostics.Log("MainWindow deactivated; moving to background.");
        bool wasPlaying = await PauseAndMuteAsync();
        await SetHostActiveAsync(isActive: false);
        SetWebViewMemoryUsageTarget(isLowPriority: true);
        SetBackgroundEfficiencyMode(enable: true);
        _viewerToggleState.Background(wasPlaying);
        CloseSettingsPopup();
        UpdateTrayMenuText();
    }

    private async void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (_isClosing
            || _isInitializingWindow
            || _isStartupRevealPending
            || _isHidingToTray
            || !_viewerToggleState.IsVisible)
        {
            return;
        }

        ResumeWebViewIfNeeded();
        await SetHostActiveAsync(isActive: true);
        SetWebViewMemoryUsageTarget(isLowPriority: false);
        SetBackgroundEfficiencyMode(enable: false);
        await RestoreFromBackgroundIfNeededAsync();
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (!_isStartupRevealPending)
        {
            return;
        }

        _isStartupRevealPending = false;
        AppDiagnostics.Log("Content rendered; forcing initial reveal.");
        await RevealCoreAsync();
    }

    private ProcessStartInfo CreateRestartStartInfo()
    {
        string? processPath = Environment.ProcessPath;
        string? entryAssemblyPath = System.Reflection.Assembly.GetEntryAssembly()?.Location;

        if (!string.IsNullOrWhiteSpace(processPath)
            && processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            return new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = $"\"{entryAssemblyPath}\"",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
        }

        string? executablePath = !string.IsNullOrWhiteSpace(processPath)
            ? processPath
            : (!string.IsNullOrWhiteSpace(entryAssemblyPath) ? entryAssemblyPath : null);

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Unable to determine the current application path for restart.");
        }

        return new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
    }

    private void TryRegisterGlobalHotkey()
    {
        AppDiagnostics.Log("Attempting global hotkey registration.");
        _hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;

        try
        {
            _hotkeyService.Register(this);
            AppDiagnostics.Log("Global hotkey registration succeeded.");
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log($"Global hotkey registration failed: {exception.Message}");
            System.Windows.MessageBox.Show(
                this,
                exception.Message,
                "ShowMeReels",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        UpdateArrowCaptureAvailability();

        if (WindowState == WindowState.Minimized)
        {
            if (_isStartupRevealPending)
            {
                WindowState = WindowState.Normal;
                return;
            }

            if (_isTaskbarMinimizeInProgress)
            {
                AppDiagnostics.Log("MainWindow minimized to taskbar.");
                _isTaskbarMinimizeInProgress = false;
                return;
            }

            await BackgroundWindowAsync();
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        CaptureCurrentBounds();
        ScheduleSettingsSave();
    }

    private async void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTaskbarMinimizeInProgress)
        {
            return;
        }

        AppDiagnostics.Log("Minimize button clicked; minimizing to taskbar.");
        bool wasPlaying = await PauseAndMuteAsync();
        await SetHostActiveAsync(isActive: false);
        _viewerToggleState.Background(wasPlaying);

        SuppressAutoHideBriefly();
        CloseSettingsPopup();
        SetWebViewVisibility(isVisible: false);
        ShowInTaskbar = true;
        UpdateTrayMenuText();

        _isTaskbarMinimizeInProgress = true;
        WindowState = WindowState.Minimized;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        await SuspendWebViewIfPossibleAsync();
        SetBackgroundEfficiencyMode(enable: true);
    }

    private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(() => _ = RevealAsync()));
    }

    private static Drawing.Icon LoadAppIcon()
    {
        StreamResourceInfo? resourceInfo = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Icon.png"));
        if (resourceInfo is null)
        {
            return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        }

        using Stream iconStream = resourceInfo.Stream;
        using MemoryStream buffer = new();
        iconStream.CopyTo(buffer);
        buffer.Position = 0;

        BitmapFrame frame = BitmapFrame.Create(
            buffer,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        buffer.Position = 0;

        using Drawing.Bitmap sourceBitmap = new(buffer);
        int iconSize = Math.Min(64, Math.Max(16, Math.Min(frame.PixelWidth, frame.PixelHeight)));
        using Drawing.Bitmap iconBitmap = new(sourceBitmap, new Drawing.Size(iconSize, iconSize));
        IntPtr hIcon = iconBitmap.GetHicon();

        try
        {
            using Drawing.Icon rawIcon = Drawing.Icon.FromHandle(hIcon);
            return (Drawing.Icon)rawIcon.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private async Task NavigateToSelectedPlatformAsync()
    {
        _settings.LastUrl = AppSettings.GetDefaultUrl(_settings.Platform);

        if (!_isWebViewReady || ReelsWebView.CoreWebView2 is null)
        {
            return;
        }

        ResumeWebViewIfNeeded();
        await SetHostActiveAsync(isActive: !_viewerToggleState.IsBackgrounded && _viewerToggleState.IsVisible);
        SetWebViewMemoryUsageTarget(isLowPriority: _viewerToggleState.IsBackgrounded || !_viewerToggleState.IsVisible);
        ReelsWebView.CoreWebView2.Navigate(_settings.LastUrl);
    }

    private async Task<bool> PauseAndMuteAsync()
    {
        if (!_isWebViewReady || ReelsWebView.CoreWebView2 is null)
        {
            return false;
        }

        string result = await ReelsWebView.CoreWebView2.ExecuteScriptAsync(_webViewScriptController.BuildPauseAndMuteScript());
        return _webViewScriptController.ParseBooleanResult(result);
    }

    private async void InstagramPlatformButton_Click(object sender, RoutedEventArgs e)
    {
        await SwitchPlatformAsync(ContentPlatform.Instagram);
    }

    private async void TikTokPlatformButton_Click(object sender, RoutedEventArgs e)
    {
        await SwitchPlatformAsync(ContentPlatform.TikTok);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        CloseSettingsPopup();

        if (_isWebViewReady && ReelsWebView.CoreWebView2 is not null)
        {
            ResumeWebViewIfNeeded();
            SetWebViewMemoryUsageTarget(isLowPriority: false);
            ReelsWebView.CoreWebView2.Reload();
            return;
        }

        _ = InitializeWebViewAsync();
    }

    private void PlaybackSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.PlaybackSpeed = Math.Round(PlaybackSpeedSlider.Value, 2);
        PlaybackSpeedValueText.Text = $"{_settings.PlaybackSpeed:0.##}x";
        _ = ApplyCurrentSettingsAsync();
        ScheduleSettingsSave();
    }

    private async void HardwareAccelerationCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.HardwareAccelerationEnabled = HardwareAccelerationCheckBox.IsChecked != false;
        await RestartForHardwareAccelerationChangeAsync();
    }

    private async void ReelsWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        await ApplyCurrentSettingsAsync();
    }

    private async void ReelsWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        WebViewMessage? message;

        try
        {
            message = JsonSerializer.Deserialize<WebViewMessage>(e.WebMessageAsJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (message is not null && string.Equals(message.Type, "seenReelDiagnostic", StringComparison.Ordinal))
        {
            AppDiagnostics.Log(FormatSeenReelDiagnostic(message));
            return;
        }

        if (!string.Equals(message?.Type, "ignoreTikTokVideo", StringComparison.Ordinal))
        {
            return;
        }

        IReadOnlyList<string> ignoreKeys = (message?.VideoIds ?? [])
            .Select(AppSettings.NormalizeTikTokIgnoreKey)
            .Where(ignoreKey => !string.IsNullOrWhiteSpace(ignoreKey))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ignoreKeys.Count == 0 && !string.IsNullOrWhiteSpace(message?.VideoId))
        {
            string? fallbackIgnoreKey = AppSettings.NormalizeTikTokIgnoreKey(message.VideoId);
            if (!string.IsNullOrWhiteSpace(fallbackIgnoreKey))
            {
                ignoreKeys = [fallbackIgnoreKey];
            }
        }

        if (ignoreKeys.Count == 0)
        {
            return;
        }

        List<string> newIgnoreKeys = ignoreKeys
            .Where(ignoreKey => !_settings.IgnoredTikTokVideoIds.Contains(ignoreKey, StringComparer.Ordinal))
            .ToList();

        if (newIgnoreKeys.Count == 0)
        {
            return;
        }

        _settings.IgnoredTikTokVideoIds.AddRange(newIgnoreKeys);
        _settings.Normalize();
        AppDiagnostics.Log($"Persisting {newIgnoreKeys.Count} ignored TikTok key(s).");
        await _settingsStore.SaveAsync(_settings);
        await ApplyCurrentSettingsAsync();
    }

    private static string FormatSeenReelDiagnostic(WebViewMessage message)
    {
        return "SeenReel "
            + $"event={message.Event ?? "unknown"} "
            + $"reason={message.Reason ?? "none"} "
            + $"reel={message.ReelId ?? "none"} "
            + $"last={message.LastActiveReelId ?? "none"} "
            + $"kind={message.IdentityKind ?? "none"} "
            + $"enabled={FormatNullable(message.SkipSeenEnabled)} "
            + $"seenBefore={FormatNullable(message.SeenBefore)} "
            + $"changed={FormatNullable(message.ActiveReelChanged)} "
            + $"overlay={FormatNullable(message.OverlayOpen)} "
            + $"suppressed={FormatNullable(message.InteractionSuppressed)} "
            + $"direction={message.SkipDirection?.ToString() ?? "none"} "
            + $"seenCount={message.SeenCount?.ToString() ?? "none"} "
            + $"visibleVideos={message.VisibleVideoCount?.ToString() ?? "none"} "
            + $"candidateCount={message.CandidateCount?.ToString() ?? "none"} "
            + $"top={FormatNullable(message.VideoTop)} "
            + $"height={FormatNullable(message.VideoHeight)} "
            + $"path={message.Path ?? "none"}";
    }

    private static string FormatNullable(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : "none";
    }

    private static string FormatNullable(double? value)
    {
        return value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "none";
    }

    private async Task RestorePlaybackAsync(bool shouldResume)
    {
        CoreWebView2? coreWebView = GetCoreWebView();
        if (coreWebView is null)
        {
            return;
        }

        ResumeWebViewIfNeeded();
        await coreWebView.ExecuteScriptAsync(_webViewScriptController.BuildResumeScript(_settings, shouldResume));
        await ApplyCurrentSettingsAsync();
    }

    private async Task RestoreFromBackgroundIfNeededAsync()
    {
        if (_isRestoringFromBackground || !_viewerToggleState.IsBackgrounded)
        {
            return;
        }

        _isRestoringFromBackground = true;

        try
        {
            AppDiagnostics.Log("Restoring playback state from background.");
            SuppressAutoHideBriefly();
            bool shouldResume = _viewerToggleState.BringToForeground();
            await RestorePlaybackAsync(shouldResume);
            UpdateTrayMenuText();
        }
        finally
        {
            _isRestoringFromBackground = false;
        }
    }

    private async Task RestartForHardwareAccelerationChangeAsync()
    {
        CloseSettingsPopup();
        _settingsSaveTimer.Stop();
        await _settingsStore.SaveAsync(_settings.Normalize());

        ProcessStartInfo startInfo = CreateRestartStartInfo();

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                this,
                $"ShowMeReels could not restart automatically.{Environment.NewLine}{exception.Message}",
                "ShowMeReels",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Close();
    }

    private async Task SwitchPlatformAsync(ContentPlatform platform)
    {
        if (_settings.Platform == platform)
        {
            ApplyPlatformSelection();
            return;
        }

        _settings.Platform = platform;
        ApplyPlatformSelection();
        CloseSettingsPopup();
        SuppressAutoHideBriefly();
        ScheduleSettingsSave();
        await NavigateToSelectedPlatformAsync();
    }

    private void ScheduleSettingsSave()
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void SeekBarCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.SeekBarEnabled = SeekBarCheckBox.IsChecked == true;
        _ = ApplyCurrentSettingsAsync();
        ScheduleSettingsSave();
    }

    private void SkipSeenReelsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.SkipSeenReelsEnabled = SkipSeenReelsCheckBox.IsChecked == true;
        _ = ApplyCurrentSettingsAsync();
        ScheduleSettingsSave();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsPopup.IsOpen)
        {
            CloseSettingsPopup();
            return;
        }

        SettingsPopup.IsOpen = true;
        SettingsButton.IsChecked = true;
        SuppressAutoHideBriefly();
    }

    private void SettingsButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!SettingsPopup.IsOpen)
        {
            return;
        }

        CloseSettingsPopup();
        e.Handled = true;
    }

    private void SettingsPopup_Closed(object? sender, EventArgs e)
    {
        SettingsButton.IsChecked = false;
    }

    private void LayoutButton_Click(object sender, RoutedEventArgs e)
    {
        if (LayoutButton.ContextMenu is null)
        {
            return;
        }

        LayoutButton.ContextMenu.PlacementTarget = LayoutButton;
        LayoutButton.ContextMenu.Placement = PlacementMode.Bottom;
        LayoutButton.ContextMenu.IsOpen = true;
        SuppressAutoHideBriefly();
    }

    private async void SettingsSaveTimer_Tick(object? sender, EventArgs e)
    {
        _settingsSaveTimer.Stop();
        await _settingsStore.SaveAsync(_settings.Normalize());
    }

    private void SnapCenterButton_Click(object sender, RoutedEventArgs e)
    {
        SnapWindow(SnapPosition.Center);
    }

    private void SnapLeftMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        SnapWindow(SnapPosition.LeftMiddle);
    }

    private void SnapLeftButton_Click(object sender, RoutedEventArgs e)
    {
        SnapWindow(SnapPosition.Left);
    }

    private void SnapRightMiddleButton_Click(object sender, RoutedEventArgs e)
    {
        SnapWindow(SnapPosition.RightMiddle);
    }

    private void SnapRightButton_Click(object sender, RoutedEventArgs e)
    {
        SnapWindow(SnapPosition.Right);
    }

    private void ResetSizeButton_Click(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        WindowBounds defaultBounds = _placementService.GetDefaultBounds(workArea);
        WindowBounds bounds = _placementService.GetSnapBounds(
            workArea,
            new System.Windows.Size(defaultBounds.Width, defaultBounds.Height),
            _settings.SnapPosition);

        ApplyWindowBounds(bounds);
    }

    private void SnapWindow(SnapPosition snapPosition)
    {
        Rect workArea = SystemParameters.WorkArea;
        System.Windows.Size currentSize = new(
            ActualWidth > 0 ? ActualWidth : Width,
            ActualHeight > 0 ? ActualHeight : Height);
        WindowBounds bounds = _placementService.GetSnapBounds(workArea, currentSize, snapPosition);

        _settings.SnapPosition = snapPosition;
        ApplyWindowBounds(bounds);
    }

    private void ApplyWindowBounds(WindowBounds bounds)
    {
        _settings.WindowBounds = bounds;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
        ScheduleSettingsSave();
    }

    private async Task ShowFromTrayAsync()
    {
        bool shouldResume = _viewerToggleState.Show();
        AppDiagnostics.Log("ShowFromTrayAsync invoked.");

        SuppressAutoHideBriefly();
        CloseSettingsPopup();
        ShowInTaskbar = true;
        SetWebViewVisibility(isVisible: true);
        Show();
        WindowState = WindowState.Normal;
        ResumeWebViewIfNeeded();
        await SetHostActiveAsync(isActive: true);
        SetWebViewMemoryUsageTarget(isLowPriority: false);
        SetBackgroundEfficiencyMode(enable: false);
        BringWindowToFront();
        UpdateArrowCaptureAvailability();

        await RestorePlaybackAsync(shouldResume);
        UpdateTrayMenuText();
    }

    public Task RevealAsync()
    {
        if (Dispatcher.CheckAccess())
        {
            return RevealCoreAsync();
        }

        return Dispatcher.InvokeAsync(RevealCoreAsync).Task.Unwrap();
    }

    private async Task RevealCoreAsync()
    {
        AppDiagnostics.Log($"RevealCoreAsync invoked. Visible={_viewerToggleState.IsVisible}; Backgrounded={_viewerToggleState.IsBackgrounded}; WindowState={WindowState}");
        if (!_viewerToggleState.IsVisible)
        {
            await ShowFromTrayAsync();
            return;
        }

        SuppressAutoHideBriefly();
        CloseSettingsPopup();
        ShowInTaskbar = true;
        SetWebViewVisibility(isVisible: true);
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        ResumeWebViewIfNeeded();
        await SetHostActiveAsync(isActive: true);
        SetWebViewMemoryUsageTarget(isLowPriority: false);
        SetBackgroundEfficiencyMode(enable: false);
        BringWindowToFront();
        UpdateArrowCaptureAvailability();
        await RestoreFromBackgroundIfNeededAsync();
    }

    private async Task ToggleVisibilityAsync()
    {
        if (!_viewerToggleState.IsVisible)
        {
            await ShowFromTrayAsync();
            return;
        }

        if (_viewerToggleState.IsBackgrounded || !IsActive)
        {
            await RevealCoreAsync();
            return;
        }

        await HideToTrayAsync();
    }

    private void SuppressAutoHideBriefly()
    {
        _autoHideSuppressedUntil = DateTimeOffset.UtcNow.Add(AutoHideSuppressionDuration);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject dependencyObject
            && (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(dependencyObject) is not null
                || FindVisualParent<Slider>(dependencyObject) is not null
                || FindVisualParent<System.Windows.Controls.CheckBox>(dependencyObject) is not null))
        {
            return;
        }

        DragMove();
    }

    private void ToggleMenuItem_Click(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => _ = ToggleVisibilityAsync()));
    }

    private void ArrowCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPinnedOnTop)
        {
            _isPinnedArrowCaptureEnabled = false;
            UpdateArrowCaptureAvailability();
            return;
        }

        _isPinnedArrowCaptureEnabled = !_isPinnedArrowCaptureEnabled;
        SuppressAutoHideBriefly();
        UpdateArrowCaptureAvailability();
    }

    private void GlobalArrowCaptureService_ArrowPressed(object? sender, GlobalArrowPressedEventArgs e)
    {
        if (!_isPinnedOnTop
            || !_isPinnedArrowCaptureEnabled
            || !_viewerToggleState.IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (!_isWebViewReady)
            {
                return;
            }

            await ExecuteWebViewCommandAsync(_webViewScriptController.BuildScrollScript(e.Direction));
        }));
    }

    private void RemoteControlServer_CommandReceived(object? sender, RemoteControlCommandEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () => await ExecuteRemoteCommandAsync(e)));
    }

    private async Task ExecuteRemoteCommandAsync(RemoteControlCommandEventArgs command)
    {
        SuppressAutoHideBriefly();

        if (!_viewerToggleState.IsVisible || WindowState == WindowState.Minimized)
        {
            await RevealCoreAsync();
        }
        else
        {
            ResumeWebViewIfNeeded();
        }

        string script = command.Command switch
        {
            RemoteControlCommand.TogglePlayPause => _webViewScriptController.BuildTogglePlayPauseScript(),
            _ => _webViewScriptController.BuildScrollScript(command.Direction),
        };

        await ExecuteWebViewCommandAsync(script);
    }

    private void TryStartRemoteControlServer()
    {
        try
        {
            _remoteControlServer.Start();
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log($"Remote control server failed to start: {exception.Message}");
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        _isPinnedOnTop = !_isPinnedOnTop;
        CloseSettingsPopup();
        SuppressAutoHideBriefly();
        ApplyPinnedTopmostState();

        if (_isPinnedOnTop)
        {
            BringWindowToFront();
        }
    }

    private void BackgroundPlaybackCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.AllowBackgroundPlayback = BackgroundPlaybackCheckBox.IsChecked == true;
        SuppressAutoHideBriefly();
        ScheduleSettingsSave();

        if (_settings.AllowBackgroundPlayback)
        {
            ResumeWebViewIfNeeded();
            SetWebViewMemoryUsageTarget(isLowPriority: false);
            SetBackgroundEfficiencyMode(enable: false);
        }
    }

    private void UpdateTrayMenuText()
    {
        _toggleMenuItem.Text = _viewerToggleState.IsVisible ? "Hide" : "Show";
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializingWindow)
        {
            return;
        }

        _settings.VolumePercent = (int)Math.Round(VolumeSlider.Value);
        VolumeValueText.Text = $"{_settings.VolumePercent}%";
        _ = ApplyCurrentSettingsAsync();
        ScheduleSettingsSave();
    }

    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsPopup.IsOpen)
        {
            CloseSettingsPopup();
            e.Handled = true;
            return;
        }

        if (!_viewerToggleState.IsVisible
            || !_isWebViewReady
            || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject dependencyObject
            && (FindVisualParent<System.Windows.Controls.Primitives.ButtonBase>(dependencyObject) is not null
                || FindVisualParent<Slider>(dependencyObject) is not null
                || FindVisualParent<System.Windows.Controls.CheckBox>(dependencyObject) is not null))
        {
            return;
        }

        string? script = e.Key switch
        {
            Key.W or Key.Up or Key.PageUp => _webViewScriptController.BuildScrollScript(-1),
            Key.S or Key.Down or Key.PageDown => _webViewScriptController.BuildScrollScript(1),
            Key.Space => _webViewScriptController.BuildTogglePlayPauseScript(),
            _ => null,
        };

        if (script is null)
        {
            return;
        }

        e.Handled = true;
        await ExecuteWebViewCommandAsync(script);
    }

    private async void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_viewerToggleState.IsVisible
            || !_isWebViewReady
            || Keyboard.Modifiers != ModifierKeys.None
            || Math.Abs(e.Delta) < 12)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject dependencyObject
            && (FindVisualParent<Slider>(dependencyObject) is not null
                || FindVisualParent<System.Windows.Controls.CheckBox>(dependencyObject) is not null))
        {
            return;
        }

        string script = e.Delta < 0
            ? _webViewScriptController.BuildScrollScript(1)
            : _webViewScriptController.BuildScrollScript(-1);
        bool moved = await ExecuteWebViewCommandWithBooleanResultAsync(script);
        e.Handled = moved;
    }

    private void SendWindowToBack()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            handle,
            HwndBottom,
            0,
            0,
            0,
            0,
            SetWindowPosFlags.IgnoreMove
                | SetWindowPosFlags.IgnoreResize
                | SetWindowPosFlags.DoNotActivate
                | SetWindowPosFlags.DoNotSendChangingEvent);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        ProcessInformationClass processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        uint processInformationSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags uFlags);

    private static readonly IntPtr HwndBottom = new(1);
    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;

    private enum ProcessInformationClass
    {
        ProcessMemoryPriority = 0,
        ProcessMemoryExhaustionInfo = 1,
        ProcessAppMemoryInfo = 2,
        ProcessInPrivateInfo = 3,
        ProcessPowerThrottling = 4,
    }

    [Flags]
    private enum SetWindowPosFlags : uint
    {
        IgnoreMove = 0x0002,
        IgnoreResize = 0x0001,
        DoNotActivate = 0x0010,
        DoNotSendChangingEvent = 0x0400,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private sealed record WebViewMessage(
        string? Type,
        string? VideoId,
        string[]? VideoIds,
        string? Event,
        string? Reason,
        string? ReelId,
        string? LastActiveReelId,
        string? IdentityKind,
        string? Path,
        bool? SkipSeenEnabled,
        bool? SeenBefore,
        bool? ActiveReelChanged,
        bool? OverlayOpen,
        bool? InteractionSuppressed,
        int? SkipDirection,
        int? SeenCount,
        int? VisibleVideoCount,
        int? CandidateCount,
        double? VideoTop,
        double? VideoHeight);
}
