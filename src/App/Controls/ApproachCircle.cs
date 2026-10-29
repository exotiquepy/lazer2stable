using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Exotique.App.Controls;

/// <summary>
/// Un hit circle façon osu! avec son cercle d'approche : le nombre de maps au centre,
/// l'anneau qui se referme sur le cercle (Approach 0 → 1) et l'éclat final (Burst 0 → 1).
/// </summary>
public sealed class ApproachCircle : Control
{
    public static readonly StyledProperty<double> ApproachProperty =
        AvaloniaProperty.Register<ApproachCircle, double>(nameof(Approach), 1.0);

    public static readonly StyledProperty<double> BurstProperty =
        AvaloniaProperty.Register<ApproachCircle, double>(nameof(Burst));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ApproachCircle, string?>(nameof(Text));

    public static readonly StyledProperty<Color> CircleColorProperty =
        AvaloniaProperty.Register<ApproachCircle, Color>(nameof(CircleColor), Color.Parse("#FF62A8"));

    public static readonly StyledProperty<Color> CircleDeepColorProperty =
        AvaloniaProperty.Register<ApproachCircle, Color>(nameof(CircleDeepColor), Color.Parse("#B8306F"));

    public static readonly StyledProperty<Color> RingColorProperty =
        AvaloniaProperty.Register<ApproachCircle, Color>(nameof(RingColor), Color.Parse("#63C9FF"));

    public static readonly StyledProperty<FontFamily> NumberFontProperty =
        AvaloniaProperty.Register<ApproachCircle, FontFamily>(nameof(NumberFont), FontFamily.Default);

    /// <summary>Avancement 0 → 1 dessiné en anneau autour du cercle ; négatif = pas d'anneau de progression.</summary>
    public static readonly StyledProperty<double> ProgressValueProperty =
        AvaloniaProperty.Register<ApproachCircle, double>(nameof(ProgressValue), -1);

    public double ProgressValue
    {
        get => GetValue(ProgressValueProperty);
        set => SetValue(ProgressValueProperty, value);
    }

    static ApproachCircle()
    {
        AffectsRender<ApproachCircle>(ApproachProperty, BurstProperty, TextProperty, ProgressValueProperty, CircleColorProperty,
            CircleDeepColorProperty, RingColorProperty, NumberFontProperty);
    }

    public double Approach
    {
        get => GetValue(ApproachProperty);
        set => SetValue(ApproachProperty, value);
    }

    public double Burst
    {
        get => GetValue(BurstProperty);
        set => SetValue(BurstProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Color CircleColor
    {
        get => GetValue(CircleColorProperty);
        set => SetValue(CircleColorProperty, value);
    }

    public Color CircleDeepColor
    {
        get => GetValue(CircleDeepColorProperty);
        set => SetValue(CircleDeepColorProperty, value);
    }

    public Color RingColor
    {
        get => GetValue(RingColorProperty);
        set => SetValue(RingColorProperty, value);
    }

    public FontFamily NumberFont
    {
        get => GetValue(NumberFontProperty);
        set => SetValue(NumberFontProperty, value);
    }

    // Rayon maximal de l'anneau d'approche, en multiples du rayon du cercle.
    private const double ApproachScale = 1.62;

    public override void Render(DrawingContext ctx)
    {
        var b = Bounds;
        double size = Math.Min(b.Width, b.Height);
        if (size <= 4) return;

        var c = new Point(b.Width / 2, b.Height / 2);
        double r = size / 2 / ApproachScale;
        double a = Math.Clamp(Approach, 0, 1);
        string text = Text ?? "";

        // Éclat final : un anneau qui s'élargit en s'effaçant.
        double burst = Math.Clamp(Burst, 0, 1);
        if (burst is > 0 and < 1)
        {
            double br = r * (1 + 0.55 * burst);
            var burstPen = new Pen(new SolidColorBrush(RingColor, 0.9 * (1 - burst)), r * 0.16 * (1 - burst) + 1);
            ctx.DrawEllipse(null, burstPen, c, br, br);
        }

        if (ProgressValue >= 0)
        {
            DrawProgressRing(ctx, c, r, Math.Clamp(ProgressValue, 0, 1));
        }
        else
        {
            // Cercle d'approche : il apparaît en se rapprochant, comme dans osu!.
            double ringR = r * (1 + (ApproachScale - 1.04) * (1 - a));
            var ringPen = new Pen(new SolidColorBrush(RingColor, 0.3 + 0.7 * a), Math.Max(2, r * 0.07));
            ctx.DrawEllipse(null, ringPen, c, ringR, ringR);
        }

        // Hit circle : dégradé, bord blanc.
        var fill = new RadialGradientBrush
        {
            Center = new RelativePoint(0.38, 0.32, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.38, 0.32, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.75, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Lighten(CircleColor, 0.18), 0),
                new GradientStop(CircleColor, 0.45),
                new GradientStop(CircleDeepColor, 1),
            },
        };

        double border = Math.Max(2, r * 0.085);
        ctx.DrawEllipse(fill, new Pen(Brushes.White, border), c, r - border / 2, r - border / 2);

        if (text.Length == 0) return;

        var typeface = new Typeface(NumberFont, FontStyle.Normal, FontWeight.ExtraBold);
        double fontSize = r * 0.82;
        FormattedText ft;
        do
        {
            ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.White);
            if (ft.Width <= r * 1.42) break;
            fontSize *= 0.92;
        } while (fontSize > 8);

        // Ombre douce sous le nombre, comme les chiffres des hit circles.
        var shadow = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize,
            new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)));
        var origin = new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2);
        ctx.DrawText(shadow, origin + new Point(0, fontSize * 0.05));
        ctx.DrawText(ft, origin);
    }

    /// <summary>Piste discrète + arc de progression, dans un dégradé rose (lazer) vers bleu (stable).</summary>
    private void DrawProgressRing(DrawingContext ctx, Point c, double r, double p)
    {
        double ringR = r * 1.36;
        double thickness = Math.Max(3, r * 0.13);

        ctx.DrawEllipse(null, new Pen(new SolidColorBrush(RingColor, 0.14), thickness), c, ringR, ringR);
        if (p <= 0.001) return;

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(CircleColor, 0), new GradientStop(RingColor, 1) },
        };
        var pen = new Pen(gradient, thickness, lineCap: PenLineCap.Round);

        if (p >= 0.999)
        {
            ctx.DrawEllipse(null, pen, c, ringR, ringR);
            return;
        }

        // Arc depuis midi, dans le sens des aiguilles d'une montre.
        double angle = p * 2 * Math.PI;
        var start = new Point(c.X, c.Y - ringR);
        var end = new Point(c.X + ringR * Math.Sin(angle), c.Y - ringR * Math.Cos(angle));
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(start, false);
            g.ArcTo(end, new Size(ringR, ringR), 0, angle > Math.PI, SweepDirection.Clockwise);
            g.EndFigure(false);
        }

        ctx.DrawGeometry(null, pen, geometry);

        // Tête de l'arc : un point lumineux, comme la bille d'un slider.
        ctx.DrawEllipse(Brushes.White, null, end, thickness * 0.32, thickness * 0.32);
    }

    private static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));
}
