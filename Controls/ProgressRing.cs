using Microsoft.Maui.Graphics;

namespace IntelligentPersonalHealthOptimization.Controls;

/// <summary>
/// A circular progress ring drawn with Microsoft.Maui.Graphics. Draws a full-circle
/// track plus a coloured arc for <see cref="Progress"/> (0–1), starting at 12 o'clock
/// and sweeping clockwise. Overlay a Label in a Grid to show the value in the centre.
/// </summary>
public class ProgressRing : GraphicsView, IDrawable
{
    public ProgressRing()
    {
        Drawable = this;
        BackgroundColor = Colors.Transparent;
    }

    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(ProgressRing), 0.0, propertyChanged: Redraw);

    public static readonly BindableProperty RingColorProperty = BindableProperty.Create(
        nameof(RingColor), typeof(Color), typeof(ProgressRing), Colors.MediumPurple, propertyChanged: Redraw);

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(ProgressRing), Color.FromArgb("#33808080"), propertyChanged: Redraw);

    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(
        nameof(Thickness), typeof(double), typeof(ProgressRing), 12.0, propertyChanged: Redraw);

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public Color RingColor { get => (Color)GetValue(RingColorProperty); set => SetValue(RingColorProperty, value); }
    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    private static void Redraw(BindableObject b, object o, object n) => ((ProgressRing)b).Invalidate();

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float t = (float)Thickness;
        float inset = t / 2f + 1f;
        var rect = new RectF(dirtyRect.X + inset, dirtyRect.Y + inset,
                             dirtyRect.Width - 2 * inset, dirtyRect.Height - 2 * inset);

        canvas.StrokeSize = t;
        canvas.StrokeLineCap = LineCap.Round;

        // Track
        canvas.StrokeColor = TrackColor;
        canvas.DrawEllipse(rect);

        // Progress arc — start at top (90°), sweep clockwise.
        double p = Math.Clamp(Progress, 0, 1);
        if (p > 0)
        {
            canvas.StrokeColor = RingColor;
            float sweep = (float)(p * 360);
            canvas.DrawArc(rect, 90f, 90f - sweep, true, false);
        }
    }
}
