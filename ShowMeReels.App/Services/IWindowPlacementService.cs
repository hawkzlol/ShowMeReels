using System.Windows;
using ShowMeReels.App.Models;

namespace ShowMeReels.App.Services;

public interface IWindowPlacementService
{
    WindowBounds GetDefaultBounds(Rect workArea);

    WindowBounds ResolveStartupBounds(Rect workArea, WindowBounds? candidate);

    WindowBounds GetSnapBounds(Rect workArea, System.Windows.Size desiredSize, SnapPosition snapPosition);

    WindowBounds SanitizeBounds(Rect workArea, WindowBounds candidate);
}
