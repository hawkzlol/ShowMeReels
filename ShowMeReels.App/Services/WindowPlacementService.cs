using System.Windows;
using ShowMeReels.App.Models;

namespace ShowMeReels.App.Services;

public sealed class WindowPlacementService : IWindowPlacementService
{
    private const double DefaultShellWidthToHeightRatio = 950.0 / 1392.0;
    private const double LegacyDefaultWidthPadding = 32;
    private const double LegacyUpgradeTolerance = 24;
    private const double MinimumViewerHeight = 640;
    private const double MinimumViewerWidth = 360;

    public WindowBounds GetDefaultBounds(Rect workArea)
    {
        double height = ClampDimension(workArea.Height, workArea.Height, MinimumViewerHeight);
        double width = ClampDimension(Math.Round(height * DefaultShellWidthToHeightRatio), workArea.Width, MinimumViewerWidth);

        return GetSnapBounds(workArea, new System.Windows.Size(width, height), SnapPosition.Center);
    }

    public WindowBounds ResolveStartupBounds(Rect workArea, WindowBounds? candidate)
    {
        if (candidate is not { IsValid: true })
        {
            return GetDefaultBounds(workArea);
        }

        WindowBounds sanitizedCandidate = SanitizeBounds(workArea, candidate);
        return LooksLikeLegacyCenteredDefault(workArea, sanitizedCandidate)
            ? GetDefaultBounds(workArea)
            : sanitizedCandidate;
    }

    public WindowBounds GetSnapBounds(Rect workArea, System.Windows.Size desiredSize, SnapPosition snapPosition)
    {
        double width = ClampDimension(desiredSize.Width, workArea.Width, MinimumViewerWidth);
        double height = ClampDimension(workArea.Height, workArea.Height, MinimumViewerHeight);
        double maximumLeft = workArea.Right - width;
        double travel = Math.Max(0, maximumLeft - workArea.Left);
        double left = snapPosition switch
        {
            SnapPosition.Left => workArea.Left,
            SnapPosition.LeftMiddle => workArea.Left + (travel * 0.25),
            SnapPosition.Center => workArea.Left + (travel * 0.5),
            SnapPosition.RightMiddle => workArea.Left + (travel * 0.75),
            SnapPosition.Right => maximumLeft,
            _ => workArea.Left + (travel * 0.5),
        };

        return new WindowBounds
        {
            Left = Math.Round(left, 0),
            Top = Math.Round(workArea.Top, 0),
            Width = Math.Round(width, 0),
            Height = Math.Round(height, 0),
        };
    }

    public WindowBounds SanitizeBounds(Rect workArea, WindowBounds candidate)
    {
        if (!candidate.IsValid)
        {
            return GetDefaultBounds(workArea);
        }

        double width = ClampDimension(candidate.Width, workArea.Width, MinimumViewerWidth);
        double height = ClampDimension(workArea.Height, workArea.Height, MinimumViewerHeight);
        double left = Math.Clamp(candidate.Left, workArea.Left, workArea.Right - width);
        double top = workArea.Top;

        return new WindowBounds
        {
            Left = Math.Round(left, 0),
            Top = Math.Round(top, 0),
            Width = Math.Round(width, 0),
            Height = Math.Round(height, 0),
        };
    }

    private static double ClampDimension(double candidate, double availableSpace, double minimumDimension)
    {
        double minimum = Math.Min(minimumDimension, availableSpace);
        return Math.Clamp(candidate, minimum, availableSpace);
    }

    private static bool IsApproximately(double candidate, double expected, double tolerance)
    {
        return Math.Abs(candidate - expected) <= tolerance;
    }

    private static double GetLegacyDefaultWidth(double height)
    {
        return Math.Round((height * 9.0 / 16.0) + LegacyDefaultWidthPadding);
    }

    private static double GetCenteredLeft(Rect workArea, double width)
    {
        return Math.Round(workArea.Left + ((workArea.Width - width) / 2), 0);
    }

    private static bool LooksLikeLegacyCenteredDefault(Rect workArea, WindowBounds candidate)
    {
        double height = ClampDimension(workArea.Height, workArea.Height, MinimumViewerHeight);
        double legacyWidth = ClampDimension(GetLegacyDefaultWidth(height), workArea.Width, MinimumViewerWidth);
        double legacyLeft = GetCenteredLeft(workArea, legacyWidth);

        return IsApproximately(candidate.Top, workArea.Top, 1)
            && IsApproximately(candidate.Height, height, 1)
            && IsApproximately(candidate.Width, legacyWidth, LegacyUpgradeTolerance)
            && IsApproximately(candidate.Left, legacyLeft, LegacyUpgradeTolerance);
    }
}
