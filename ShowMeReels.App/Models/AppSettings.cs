using System.Text.RegularExpressions;

namespace ShowMeReels.App.Models;

public sealed class AppSettings
{
    private static readonly Regex TikTokVideoIdRegex = new("/video/([^/?#]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public const string DefaultInstagramUrl = "https://www.instagram.com/reels/";
    public const string DefaultTikTokUrl = "https://www.tiktok.com/";
    public const string DefaultLastUrl = DefaultInstagramUrl;
    public const double DefaultPlaybackSpeed = 1.0;
    public const int DefaultVolumePercent = 50;
    public const double MaxPlaybackSpeed = 10.0;
    public const double MinPlaybackSpeed = 0.25;

    public string LastUrl { get; set; } = DefaultLastUrl;

    public double PlaybackSpeed { get; set; } = DefaultPlaybackSpeed;

    public ContentPlatform Platform { get; set; } = ContentPlatform.Instagram;

    public SnapPosition SnapPosition { get; set; } = SnapPosition.Center;

    public bool SeekBarEnabled { get; set; }

    public bool HardwareAccelerationEnabled { get; set; } = true;

    public bool AllowBackgroundPlayback { get; set; }

    public List<string> IgnoredTikTokVideoIds { get; set; } = [];

    public bool SkipSeenReelsEnabled { get; set; }

    public int VolumePercent { get; set; } = DefaultVolumePercent;

    public WindowBounds? WindowBounds { get; set; }

    public static string GetDefaultUrl(ContentPlatform platform)
    {
        return platform == ContentPlatform.TikTok ? DefaultTikTokUrl : DefaultInstagramUrl;
    }

    public AppSettings Normalize()
    {
        PlaybackSpeed = Math.Clamp(PlaybackSpeed, MinPlaybackSpeed, MaxPlaybackSpeed);
        VolumePercent = Math.Clamp(VolumePercent, 0, 100);
        IgnoredTikTokVideoIds = (IgnoredTikTokVideoIds ?? [])
            .Select(NormalizeTikTokIgnoreKey)
            .Where(videoId => !string.IsNullOrWhiteSpace(videoId))
            .Distinct(StringComparer.Ordinal)
            .ToList()!;
        string defaultUrl = GetDefaultUrl(Platform);

        if (WindowBounds is { IsValid: false })
        {
            WindowBounds = null;
        }

        if (string.IsNullOrWhiteSpace(LastUrl) || !Uri.TryCreate(LastUrl, UriKind.Absolute, out Uri? uri))
        {
            LastUrl = defaultUrl;
        }
        else if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            LastUrl = defaultUrl;
        }
        else if (!MatchesPlatform(Platform, uri))
        {
            LastUrl = defaultUrl;
        }

        return this;
    }

    public static string? NormalizeTikTokVideoId(string? candidate)
    {
        string? normalizedKey = NormalizeTikTokIgnoreKey(candidate);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return null;
        }

        return normalizedKey.StartsWith("fp:", StringComparison.OrdinalIgnoreCase)
            || normalizedKey.StartsWith("asset:", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalizedKey;
    }

    public static string? NormalizeTikTokIgnoreKey(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string trimmed = candidate.Trim();
        if (trimmed.StartsWith("fp:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("asset:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = trimmed.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return $"{parts[0].ToLowerInvariant()}:{parts[1].ToLowerInvariant()}";
            }

            return null;
        }

        Match match = TikTokVideoIdRegex.Match(trimmed);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return trimmed
            .All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? trimmed
            : null;
    }

    private static bool MatchesPlatform(ContentPlatform platform, Uri uri)
    {
        string host = uri.Host;
        return platform switch
        {
            ContentPlatform.TikTok => host.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase),
            _ => host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase),
        };
    }
}
