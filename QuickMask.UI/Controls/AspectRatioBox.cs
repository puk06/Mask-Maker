using Avalonia;
using Avalonia.Controls;

namespace QuickMask.UI.Controls;

/// <summary>Arranges its child at the largest centered rectangle that matches the given width/height ratio.</summary>
public class AspectRatioBox : Decorator
{
    public static readonly StyledProperty<double> RatioProperty =
        AvaloniaProperty.Register<AspectRatioBox, double>(nameof(Ratio), 1.0);

    public double Ratio
    {
        get => GetValue(RatioProperty);
        set => SetValue(RatioProperty, value);
    }

    static AspectRatioBox()
    {
        RatioProperty.Changed.AddClassHandler<AspectRatioBox>((box, _) => box.InvalidateMeasure());
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(FitSize(availableSize, Ratio));
        return Child?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = FitSize(finalSize, Ratio);
        Child?.Arrange(new Rect(new Point((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2), size));

        return finalSize;
    }

    private static Size FitSize(Size available, double ratio)
    {
        if (ratio <= 0 || double.IsNaN(ratio) || double.IsInfinity(available.Width) || double.IsInfinity(available.Height))
            return available;

        if (available.Width / available.Height > ratio)
        {
            var width = Math.Min(available.Width, available.Height * ratio);
            return new Size(width, width / ratio);
        }

        var height = Math.Min(available.Height, available.Width / ratio);
        return new Size(height * ratio, height);
    }
}
