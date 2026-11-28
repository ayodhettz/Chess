using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using ChessLogic;

namespace ChessUI;

public sealed class ReserveBar : WrapPanel
{
    private readonly Dictionary<PieceType, (Button Button, Image Image, TextBlock Count)> entries = new();
    private Action<PieceType>? select;

    public ReserveBar()
    {
        foreach (PieceType type in new[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen })
        {
            Image image = new() { Width = 24, Height = 24 };
            TextBlock count = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) };
            StackPanel content = new() { Orientation = Orientation.Horizontal };
            content.Children.Add(image); content.Children.Add(count);
            Button button = new() { Content = content, MinWidth = 48, MinHeight = 44, Padding = new Thickness(4), Margin = new Thickness(0, 0, 6, 6), IsEnabled = false };
            button.SetResourceReference(StyleProperty, "ButtonStyle");
            button.Click += (_, _) => select?.Invoke(type);
            entries.Add(type, (button, image, count)); Children.Add(button);
        }
    }

    public void Update(Player owner, Func<PieceType, int> count, bool canSelect, Action<PieceType> onSelect)
    {
        select = onSelect;
        foreach (var (type, entry) in entries)
        {
            int available = count(type);
            entry.Image.Source = Images.GetImage(type, owner);
            entry.Count.Text = available.ToString();
            entry.Button.IsEnabled = canSelect && available > 0;
            AutomationProperties.SetName(entry.Button, $"{owner} reserve {type}, {available} available");
            entry.Button.ToolTip = $"{type}: {available} · select, then choose a highlighted square";
        }
    }
}
