using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ChessLogic;
using ChessUI;

namespace Chess.Tests;

[Collection("WPF")]
public class CrazyhouseUiTests(WpfFixture fixture)
{
    private static object? Invoke(MainWindow window, string method, params object[] args) =>
        typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);

    [Fact]
    public void SetupDefaultsToStandardAndOffersCrazyhouse() => fixture.Run(() =>
    {
        StartWindow window = new();
        try
        {
            Assert.Equal(GameVariant.Standard, typeof(StartWindow).GetProperty("SelectedVariant", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window));
            ((RadioButton)window.FindName("CrazyhouseRadio")).IsChecked = true;
            Assert.Equal(GameVariant.Crazyhouse, typeof(StartWindow).GetProperty("SelectedVariant", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window));
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(Player.White, "4k3/8/8/8/8/8/8/4K3[P] w - - 0 1", 4, 4)]
    [InlineData(Player.Black, "4k3/8/8/8/8/8/8/4K3[p] b - - 0 1", 3, 4)]
    public void NativeReserveButtonsDropForEitherColourAndUndoRestoresThem(Player human, string fen, int row, int column) => fixture.Run(() =>
    {
        GameState game = Fen.Parse(fen);
        MainWindow window = new(human, game);
        try
        {
            Assert.Equal(Visibility.Visible, ((StackPanel)window.FindName("ReservePanel")).Visibility);
            Button pawn = (Button)((ReserveBar)window.FindName("OwnReserve")).Children[0];
            Assert.True(pawn.IsEnabled);
            pawn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Invoke(window, "OnToPositionSelected", new Position(row, column));
            Assert.Equal(human, game.Board.GetPiece(new(row, column)).Colour);
            Assert.Equal(0, game.Board.PocketCount(human, PieceType.Pawn));
            Assert.False(pawn.IsEnabled);
            game.Undo(); Invoke(window, "UpdateStatus");
            Assert.True(pawn.IsEnabled);
            Invoke(window, "RestartGame");
            Assert.Contains("Crazyhouse", ((TextBlock)window.FindName("MatchTitleText")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void StandardGamesKeepReservesHidden() => fixture.Run(() =>
    {
        MainWindow window = new();
        try { Assert.Equal(Visibility.Collapsed, ((StackPanel)window.FindName("ReservePanel")).Visibility); }
        finally { window.Close(); }
    });
}
