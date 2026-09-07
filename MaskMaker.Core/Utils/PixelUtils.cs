namespace MaskMaker.Core.Utils;

public static class PixelUtils
{
    public static int GetPixelIndex(int x, int y, int width) => (y * width) + x;
}
