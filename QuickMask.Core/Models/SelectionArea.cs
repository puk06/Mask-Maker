namespace QuickMask.Core.Models;

public class SelectionArea(int width, int height)
{
    public PixelMask Mask { get; } = new(width * height);
    public int PixelCount { get; set; }
    public bool IsErase { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Width { get; } = width;
    public int Height { get; } = height;
}
