using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using ChessLogic;
using ChessUI;

namespace Chess.Tests;

[Collection("WPF")]
public class BoardFeedbackTests(WpfFixture fixture)
{
    [Fact]
    public void FinishedSaveLoadedOverAnActiveGameDoesNotAnimate() => fixture.Run(() =>
    {
        MainWindow window = new(Player.White);
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"chess-result-{Guid.NewGuid():N}.chess.json");
        try
        {
            GameState saved = Fen.Parse(Fen.InitialPosition); saved.Resign(Player.Black);
            GameFiles.Save(path, saved);
            typeof(MainWindow).GetMethod("LoadGameFromFile", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [path]);
            GameOverMenu menu = Assert.IsType<GameOverMenu>(((ContentControl)window.FindName("MenuContainer")).Content);
            menu.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal("You win", ((TextBlock)menu.FindName("Winner_Text")).Text);
            Assert.False(((Border)menu.FindName("ResultPanel")).HasAnimatedProperties);
        }
        finally { window.Close(); System.IO.File.Delete(path); }
    });

    [Theory]
    [InlineData(Player.White, 60)]
    [InlineData(Player.Black, 3)]
    public void CheckTintSurvivesClearingMovesInEitherOrientation(Player human, int index) => fixture.Run(() =>
    {
        MainWindow window = new(human, Fen.Parse("4k3/8/8/8/8/8/4r3/4K3 w - - 0 1"));
        try
        {
            UniformGrid checks = (UniformGrid)window.FindName("CheckGrid");
            Rectangle checkedSquare = (Rectangle)checks.Children[index];
            Assert.True(Assert.IsType<SolidColorBrush>(checkedSquare.Fill).Color.R > 150);
            typeof(MainWindow).GetMethod("ClearHighlights", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert.True(Assert.IsType<SolidColorBrush>(checkedSquare.Fill).Color.A > 0);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void RestoredResultDoesNotAnimate() => fixture.Run(() =>
    {
        GameState game = Fen.Parse(Fen.InitialPosition); game.Resign(Player.Black);
        GameOverMenu menu = new(game, Player.White, animate: false);
        menu.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Assert.False(((Border)menu.FindName("ResultPanel")).HasAnimatedProperties);
    });

    [Fact]
    public void ComputerResultUsesTheHumanPerspectiveAndLocalNamesTheWinner() => fixture.Run(() =>
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        game.Resign(Player.Black);
        Assert.Equal("You win", ((TextBlock)new GameOverMenu(game, Player.White).FindName("Winner_Text")).Text);
        Assert.Equal("You lose", ((TextBlock)new GameOverMenu(game, Player.Black).FindName("Winner_Text")).Text);
        Assert.Equal("White Wins!", ((TextBlock)new GameOverMenu(game).FindName("Winner_Text")).Text);
    });
}
