namespace ShowMeReels.App.Models;

public sealed record class WindowBounds
{
    public double Height { get; set; }

    public bool IsValid => Width > 0 && Height > 0;

    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }
}
