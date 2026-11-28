using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ChessUI;

internal static class ResultMotion
{
    internal static void Show(Border panel, bool won, bool draw, bool animate = true)
    {
        Color color = (Color)ColorConverter.ConvertFromString(draw ? "#B7B5AD" : won ? "#CBB17C" : "#D8776C");
        panel.BorderBrush = new SolidColorBrush(color);
        if (!animate || !SystemParameters.ClientAreaAnimation) return;

        // One short arrival; the result stays readable and controls remain usable.
        panel.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.65, 1, TimeSpan.FromMilliseconds(450)));
        if (draw) return;
        TranslateTransform shift = new(0, won ? -4 : 4);
        panel.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }
}
