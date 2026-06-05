using System.Windows;
using ShowMeReels.App.Models;
using ShowMeReels.App.Services;

namespace ShowMeReels.Tests;

public sealed class WindowPlacementServiceTests
{
    [Theory]
    [InlineData(SnapPosition.Left, 0)]
    [InlineData(SnapPosition.LeftMiddle, 355)]
    [InlineData(SnapPosition.Center, 710)]
    [InlineData(SnapPosition.RightMiddle, 1065)]
    [InlineData(SnapPosition.Right, 1420)]
    public void GetSnapBounds_AlignsWindowToRequestedHorizontalPosition(SnapPosition snapPosition, double expectedLeft)
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 1920, 1040);

        WindowBounds bounds = service.GetSnapBounds(workArea, new Size(500, 880), snapPosition);

        Assert.Equal(expectedLeft, bounds.Left, 1);
        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(500, bounds.Width, 1);
        Assert.Equal(1040, bounds.Height, 1);
    }

    [Fact]
    public void GetDefaultBounds_FillsTheWorkAreaHeight()
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 1920, 1040);

        WindowBounds bounds = service.GetDefaultBounds(workArea);

        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(1040, bounds.Height, 1);
    }

    [Fact]
    public void GetDefaultBounds_UsesWiderShellDefaultRatio()
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 3000, 1392);

        WindowBounds bounds = service.GetDefaultBounds(workArea);

        Assert.Equal(1025, bounds.Left, 1);
        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(950, bounds.Width, 1);
        Assert.Equal(1392, bounds.Height, 1);
    }

    [Fact]
    public void ResolveStartupBounds_ReplacesLegacyCenteredDefaultWithCurrentDefault()
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 1920, 1040);
        WindowBounds candidate = new()
        {
            Left = 652,
            Top = 0,
            Width = 617,
            Height = 1040,
        };

        WindowBounds bounds = service.ResolveStartupBounds(workArea, candidate);

        Assert.Equal(605, bounds.Left, 1);
        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(710, bounds.Width, 1);
        Assert.Equal(1040, bounds.Height, 1);
    }

    [Fact]
    public void ResolveStartupBounds_PreservesCustomSavedBounds()
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 2568, 1392);
        WindowBounds candidate = new()
        {
            Left = 809,
            Top = 0,
            Width = 950,
            Height = 1392,
        };

        WindowBounds bounds = service.ResolveStartupBounds(workArea, candidate);

        Assert.Equal(809, bounds.Left, 1);
        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(950, bounds.Width, 1);
        Assert.Equal(1392, bounds.Height, 1);
    }

    [Fact]
    public void SanitizeBounds_ClampsWindowInsideWorkArea()
    {
        WindowPlacementService service = new();
        Rect workArea = new(0, 0, 1200, 800);
        WindowBounds candidate = new()
        {
            Left = 1100,
            Top = -20,
            Width = 500,
            Height = 900,
        };

        WindowBounds bounds = service.SanitizeBounds(workArea, candidate);

        Assert.Equal(700, bounds.Left, 1);
        Assert.Equal(0, bounds.Top, 1);
        Assert.Equal(500, bounds.Width, 1);
        Assert.Equal(800, bounds.Height, 1);
    }
}
