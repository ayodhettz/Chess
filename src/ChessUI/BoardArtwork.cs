using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ChessLogic;

namespace ChessUI;

internal static class BoardArtwork
{
    internal static void FillSquares(UniformGrid grid)
    {
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                grid.Children.Add(new Border { Background = (Brush)Application.Current.FindResource((row + col) % 2 == 0 ? "LightSquareColor" : "DarkSquareColor") });
    }

    internal static void FillCoordinates(UniformGrid ranks, UniformGrid files, bool flipped)
    {
        for (int index = 0; index < 8; index++)
        {
            ranks.Children.Add(Label((flipped ? index + 1 : 8 - index).ToString()));
            files.Children.Add(Label(((char)('a' + (flipped ? 7 - index : index))).ToString()));
        }
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text, FontFamily = (FontFamily)Application.Current.FindResource("MonoFont"), FontSize = 12,
        Foreground = (Brush)Application.Current.FindResource("MutedColor"),
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
}
