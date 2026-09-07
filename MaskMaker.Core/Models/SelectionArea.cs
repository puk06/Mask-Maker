using System.Collections;

namespace MaskMaker.Core.Models;

public class SelectionArea(int width, int height)
{
    public BitArray Mask { get; } = new BitArray(width * height);
    public bool IsErase { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Width { get; } = width;
    public int Height { get; } = height;
}
