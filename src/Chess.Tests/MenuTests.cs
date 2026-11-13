using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using ChessLogic;
using ChessUI;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class MenuTests(WpfFixture fixture) : IClassFixture<WpfFixture>
{
    [Fact]
    public void BoardAndHistoryFitTheWindow() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Board.Initial(), Player.White);
            Play(game, "e2", "e4"); Play(game, "e7", "e5");
            Play(game, "g1", "f3"); Play(game, "b8", "c6");
            SetGame(window, game);
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(844, 600));
            root.Arrange(new Rect(0, 0, 844, 600));
            root.UpdateLayout();

            ListBox history = (ListBox)window.FindName("HistoryList");
            Assert.Equal(4, history.Items.Count);
            Assert.True(history.ActualHeight > 200);
            Assert.True(((Grid)window.FindName("BoardGrid")).ActualWidth > 0);

            string? screenshotPath = Environment.GetEnvironmentVariable("CHESS_SCREENSHOT_PATH");
            if (screenshotPath != null)
            {
                RenderTargetBitmap bitmap = new(844, 600, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using Stream output = File.Create(screenshotPath);
                encoder.Save(output);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public void IntendedDrawClaimEndsTheGameBeforePlayingTheMove() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Board.Initial(), Player.White);
            KnightCycle(game);
            Play(game, "g1", "f3"); Play(game, "g8", "f6"); Play(game, "f3", "g1");
            SetGame(window, game);
            ((CheckBox)window.FindName("ClaimWithMoveCheckBox")).IsChecked = true;

            Call(window, "HandleMove", FindMove(game, "f6", "g8"));

            Assert.Equal(EndReason.ThreefoldRepetition, game.Result!.Reason);
            Assert.Equal(7, game.History.Count);
            Assert.IsType<Knight>(game.Board[At("f6")]);
            Assert.IsType<GameOverMenu>(Menu(window).Content);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void EscapeOnlyDismissesThePauseMenu() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            Escape(window);
            Assert.IsType<PauseMenu>(Menu(window).Content);
            Escape(window);
            Assert.Null(Menu(window).Content);

            Call(window, "Resign_Click", window, new RoutedEventArgs());
            object menu = Menu(window).Content;
            Assert.IsType<GameOverMenu>(menu);
            Escape(window);
            Assert.Same(menu, Menu(window).Content);
            Assert.False(((StackPanel)window.FindName("ActionPanel")).IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void PromotionCannotBeDismissedAndCommitsTheChosenPieceOnce() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Parse("4k3/P7/8/8/8/8/8/4K3"), Player.White);
            SetGame(window, game);
            Call(window, "HandlePromotion", At("a7"), At("a8"));
            PromotionMenu menu = Assert.IsType<PromotionMenu>(Menu(window).Content);
            Escape(window);
            Assert.Same(menu, Menu(window).Content);
            Assert.Empty(game.History);
            Assert.IsType<Pawn>(game.Board[At("a7")]);

            MouseButtonEventArgs click = new(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent };
            ((Image)menu.FindName("QueenImg")).RaiseEvent(click);

            Assert.True(click.Handled);
            Assert.IsType<Queen>(game.Board[At("a8")]);
            Assert.Single(game.History);
            Assert.Null(Menu(window).Content);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void RestartClearsHistoryAndResetsDrawControls() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Board.Initial(), Player.White);
            SetGame(window, game);
            Call(window, "HandleMove", FindMove(game, "e2", "e4"));
            Assert.Single(((ListBox)window.FindName("HistoryList")).Items.Cast<object>());
            ((CheckBox)window.FindName("ClaimWithMoveCheckBox")).IsChecked = true;

            Call(window, "RestartGame");

            Assert.Empty(((ListBox)window.FindName("HistoryList")).Items.Cast<object>());
            Assert.False(((CheckBox)window.FindName("ClaimWithMoveCheckBox")).IsChecked);
            Assert.False(((Button)window.FindName("ClaimDrawButton")).IsEnabled);
            Assert.Equal("White to move", ((TextBlock)window.FindName("StatusText")).Text);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void InvalidPromotionDrawClaimRestoresTheDisplayedBoard() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Parse("4k3/P7/8/8/8/8/8/4K3"), Player.White);
            SetGame(window, game);
            ((CheckBox)window.FindName("ClaimWithMoveCheckBox")).IsChecked = true;
            Call(window, "HandlePromotion", At("a7"), At("a8"));
            PromotionMenu menu = Assert.IsType<PromotionMenu>(Menu(window).Content);
            ((Image)menu.FindName("QueenImg")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent });

            Assert.Empty(game.History);
            Assert.IsType<Pawn>(game.Board[At("a7")]);
            Assert.Null(game.Board[At("a8")]);
            Assert.Null(Menu(window).Content);
            Image[,] images = (Image[,])Field(window, "pieceImages");
            Assert.NotNull(images[1, 0].Source);
            Assert.Null(images[0, 0].Source);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void DrawClaimButtonEndsARepeatedPosition() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = new(Board.Initial(), Player.White);
            KnightCycle(game); KnightCycle(game);
            SetGame(window, game);
            Assert.True(((Button)window.FindName("ClaimDrawButton")).IsEnabled);

            Call(window, "ClaimDraw_Click", window, new RoutedEventArgs());

            Assert.Equal(EndReason.ThreefoldRepetition, game.Result!.Reason);
            Assert.IsType<GameOverMenu>(Menu(window).Content);
        }
        finally { window.Close(); }
    });

    private static ContentControl Menu(MainWindow window) => (ContentControl)window.FindName("MenuContainer");

    private static void SetGame(MainWindow window, GameState game)
    {
        typeof(MainWindow).GetField("gameState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, game);
        Call(window, "DrawBoard", game.Board);
        Call(window, "UpdateStatus");
    }

    private static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void Escape(MainWindow window)
    {
        KeyEventArgs key = new(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(key);
        Assert.True(key.Handled);
    }

    private static void Call(object target, string method, params object[] arguments)
    {
        try
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
