using ShowMeReels.App.Models;
using ShowMeReels.App.Services;

namespace ShowMeReels.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        using TempDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.json");
        JsonSettingsStore store = new(settingsPath);

        AppSettings settings = await store.LoadAsync();

        Assert.Equal(AppSettings.DefaultPlaybackSpeed, settings.PlaybackSpeed);
        Assert.Equal(AppSettings.DefaultVolumePercent, settings.VolumePercent);
        Assert.Equal(AppSettings.DefaultLastUrl, settings.LastUrl);
        Assert.Equal(ContentPlatform.Instagram, settings.Platform);
        Assert.Equal(SnapPosition.Center, settings.SnapPosition);
        Assert.True(settings.HardwareAccelerationEnabled);
        Assert.False(settings.AllowBackgroundPlayback);
        Assert.Empty(settings.IgnoredTikTokVideoIds);
        Assert.False(settings.SkipSeenReelsEnabled);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsSettings()
    {
        using TempDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.json");
        JsonSettingsStore store = new(settingsPath);
        AppSettings expected = new()
        {
            PlaybackSpeed = 2.5,
            VolumePercent = 73,
            SeekBarEnabled = true,
            HardwareAccelerationEnabled = false,
            AllowBackgroundPlayback = true,
            IgnoredTikTokVideoIds = ["7528162394021111111", "7528162394022222222"],
            SkipSeenReelsEnabled = true,
            WindowBounds = new WindowBounds
            {
                Left = 88,
                Top = 0,
                Width = 520,
                Height = 900,
            },
            SnapPosition = SnapPosition.Right,
            Platform = ContentPlatform.TikTok,
            LastUrl = "https://www.tiktok.com/@showmereels/video/7528162394021111111",
        };

        await store.SaveAsync(expected);
        AppSettings actual = await store.LoadAsync();

        Assert.Equal(expected.PlaybackSpeed, actual.PlaybackSpeed);
        Assert.Equal(expected.VolumePercent, actual.VolumePercent);
        Assert.Equal(expected.SeekBarEnabled, actual.SeekBarEnabled);
        Assert.Equal(expected.HardwareAccelerationEnabled, actual.HardwareAccelerationEnabled);
        Assert.Equal(expected.AllowBackgroundPlayback, actual.AllowBackgroundPlayback);
        Assert.Equal(expected.IgnoredTikTokVideoIds, actual.IgnoredTikTokVideoIds);
        Assert.Equal(expected.SkipSeenReelsEnabled, actual.SkipSeenReelsEnabled);
        Assert.Equal(expected.WindowBounds, actual.WindowBounds);
        Assert.Equal(expected.SnapPosition, actual.SnapPosition);
        Assert.Equal(expected.Platform, actual.Platform);
        Assert.Equal(expected.LastUrl, actual.LastUrl);
    }

    [Fact]
    public void NormalizeTikTokVideoId_ExtractsCanonicalVideoId()
    {
        Assert.Equal(
            "7528162394021111111",
            AppSettings.NormalizeTikTokVideoId("https://www.tiktok.com/@showmereels/video/7528162394021111111?lang=en"));
        Assert.Equal(
            "7528162394021111111",
            AppSettings.NormalizeTikTokVideoId("7528162394021111111"));
        Assert.Null(AppSettings.NormalizeTikTokVideoId("not a valid/video id"));
    }

    [Fact]
    public void NormalizeTikTokIgnoreKey_AllowsFallbackFingerprintKeys()
    {
        Assert.Equal("fp:abcd1234", AppSettings.NormalizeTikTokIgnoreKey("fp:ABCD1234"));
        Assert.Equal("asset:feedbeef", AppSettings.NormalizeTikTokIgnoreKey("asset:FEEDBEEF"));
        Assert.Null(AppSettings.NormalizeTikTokIgnoreKey("fp:"));
    }
}
