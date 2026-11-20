using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using ChessLogic;
using ChessUI;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

[Collection("WPF")]
public class AutoBotTests(WpfFixture fixture)
{
    [Fact]
    public void DenseDrawControlsRemainScrollableAtMinimumSize() => fixture.Run(() =>
    {
        MainWindow window = new();
        try
        {
            GameState game = Fen.Parse(Fen.InitialPosition.Replace("0 1", "100 42"));
            game.OfferDraw();
            Call(window, "LoadGame", game);
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(852, 620)); root.Arrange(new Rect(0, 0, 852, 620)); root.UpdateLayout();
            Assert.True(((ListBox)window.FindName("HistoryList")).ActualHeight > 40);
            ScrollViewer actions = (ScrollViewer)window.FindName("ActionsScrollViewer");
            Assert.True(actions.ScrollableHeight > 0);
            actions.ScrollToEnd(); root.UpdateLayout();
            Assert.Equal(actions.ScrollableHeight, actions.VerticalOffset);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void StartScreenDefaultsToBotAndOffersBothColoursAndLocalPlay() => fixture.Run(() =>
    {
        StartWindow start = new();
        try
        {
            Assert.Equal(Player.White, SelectedSide(start));
            ((RadioButton)start.FindName("BlackRadio")).IsChecked = true;
            Assert.Equal(Player.Black, SelectedSide(start));
            ((RadioButton)start.FindName("LocalModeRadio")).IsChecked = true;
            Assert.Null(SelectedSide(start));
            Assert.Equal(Visibility.Collapsed, ((StackPanel)start.FindName("ColourPanel")).Visibility);
            ((RadioButton)start.FindName("BotModeRadio")).IsChecked = true;
            Assert.Equal(Player.Black, SelectedSide(start));
            ((RadioButton)start.FindName("WhiteRadio")).IsChecked = true;
            Preview(start, "CHESS_START_PREVIEW_PATH", 920, 620);
        }
        finally { start.Close(); }
    });

    [Fact]
    public void StartAndNewGameNavigateBetweenWindowsAndStartTheBotOnLoad() => fixture.Run(() =>
    {
        Window previous = Application.Current.MainWindow;
        StartWindow start = new();
        MainWindow? game = null;
        StartWindow? returned = null;
        try
        {
            ((RadioButton)start.FindName("BlackRadio")).IsChecked = true;
            Call(start, "StartGame_Click", start, new RoutedEventArgs());
            game = Assert.IsType<MainWindow>(Application.Current.MainWindow);
            game.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
            FinishAutomatic(game);
            Assert.Single(Game(game).History);
            Assert.Equal(Player.Black, Game(game).CurrentPlayer);
            Call(game, "NewGame_Click", game, new RoutedEventArgs());
            returned = Assert.IsType<StartWindow>(Application.Current.MainWindow);
            Assert.True(returned.IsVisible);
            Assert.True((bool)Field(game, "windowClosed")!);
        }
        finally
        {
            returned?.Close(); game?.Close(); start.Close();
            Application.Current.MainWindow = previous;
        }
    });

    [Fact]
    public void HumanWhiteGetsExactlyOneAutomaticReplyAndRoundUndoRedo() => fixture.Run(() =>
    {
        MainWindow window = new(Player.White);
        try
        {
            StartSession(window);
            Assert.Empty(Game(window).History);
            Call(window, "HandleMove", FindMove(Game(window), "e2", "e4"));
            FinishAutomatic(window);
            Assert.Equal(2, Game(window).History.Count);
            Assert.Equal(Player.White, Game(window).CurrentPlayer);
            Assert.True(((StackPanel)window.FindName("ActionPanel")).IsEnabled);
            string after = Game(window).ToFen();
            Call(window, "NavigateHistory", false);
            Assert.Empty(Game(window).History);
            Assert.Equal(Player.White, Game(window).CurrentPlayer);
            Call(window, "NavigateHistory", true);
            Assert.Equal(after, Game(window).ToFen());
            Assert.Equal(2, Game(window).History.Count);
            Preview(window, "CHESS_AUTO_PREVIEW_PATH", 1032, 740);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void BlackGetsABotOpeningAndTheBoardFacesBlack() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            Image[,] pieces = (Image[,])Field(window, "pieceImages")!;
            Assert.Same(pieces[7, 7], ((System.Windows.Controls.Primitives.UniformGrid)window.FindName("PieceGrid")).Children[0]);
            Assert.Equal(At("h1"), Call(window, "DisplaySquare", 0, 0));
            StartSession(window);
            FinishAutomatic(window);
            Assert.Single(Game(window).History);
            Assert.Equal(Player.Black, Game(window).CurrentPlayer);
            Preview(window, "CHESS_BLACK_PREVIEW_PATH", 1032, 740);
            Call(window, "NavigateHistory", false);
            Assert.Empty(Game(window).History);
            Assert.Equal(Visibility.Visible, ((Button)window.FindName("ResumeBotButton")).Visibility);
            Call(window, "ResumeBot_Click", window, new RoutedEventArgs());
            FinishAutomatic(window);
            Assert.Single(Game(window).History);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void HumanCannotPlayOrResignOnTheBotsTurn() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            string before = GameSave.Serialize(Game(window));
            Call(window, "HandleMove", FindMove(Game(window), "e2", "e4"));
            Call(window, "Resign_Click", window, new RoutedEventArgs());
            Call(window, "OnFromPositionSelected", At("e2"));
            Assert.Equal(before, GameSave.Serialize(Game(window)));
            Assert.Null(Field(window, "selectedPosition"));
            Assert.False(((Button)window.FindName("SuggestMoveButton")).IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void UndoDuringThinkingDiscardsThePendingReply() => fixture.Run(() =>
    {
        MainWindow window = new(Player.White);
        try
        {
            StartSession(window);
            string before = Game(window).ToFen();
            Call(window, "HandleMove", FindMove(Game(window), "e2", "e4"));
            Task oldSearch = (Task)Field(window, "botTurnTask")!;
            Call(window, "NavigateHistory", false);
            Finish(oldSearch);
            Assert.Equal(before, Game(window).ToFen());
            Assert.Empty(Game(window).History);
            Assert.True(Game(window).CanRedo);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void CancellingLeavesAnExplicitResumeActionWithoutRestartingTheSearch() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            StartSession(window);
            Task search = (Task)Field(window, "botTurnTask")!;
            Call(window, "CancelBot_Click", window, new RoutedEventArgs());
            Finish(search);
            Assert.Empty(Game(window).History);
            Assert.Equal(Visibility.Visible, ((Button)window.FindName("ResumeBotButton")).Visibility);
            Assert.Same(search, Field(window, "botTurnTask"));
            Call(window, "ResumeBot_Click", window, new RoutedEventArgs());
            FinishAutomatic(window);
            Assert.Single(Game(window).History);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void PauseCancelsTheReplyAndContinueStartsAFreshSearch() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            StartSession(window);
            Task old = (Task)Field(window, "botTurnTask")!;
            Escape(window);
            Finish(old);
            Assert.IsType<PauseMenu>(((ContentControl)window.FindName("MenuContainer")).Content);
            Assert.Empty(Game(window).History);
            Escape(window);
            FinishAutomatic(window);
            Assert.Single(Game(window).History);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void SuggestionsHighlightWithoutChangingTheGameOrItsHistory() => fixture.Run(() =>
    {
        MainWindow window = new(Player.White);
        try
        {
            StartSession(window);
            string before = GameSave.Serialize(Game(window));
            Finish((Task)Call(window, "SuggestMoveAsync")!);
            Assert.Equal(before, GameSave.Serialize(Game(window)));
            Assert.StartsWith("Suggestion:", ((TextBlock)window.FindName("BotStatusText")).Text);
            Rectangle[,] highlights = (Rectangle[,])Field(window, "highlights")!;
            Assert.Equal(2, highlights.Cast<Rectangle>().Count(rectangle => rectangle.Fill is SolidColorBrush brush && brush.Color.A > 0));
            Preview(window, "CHESS_SUGGEST_PREVIEW_PATH", 1032, 740);
            Call(window, "OnFromPositionSelected", At("e2"));
            Assert.NotNull(Field(window, "selectedPosition"));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void LoadingAnotherPositionDuringSearchCannotReceiveTheOldMove() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            StartSession(window);
            Task old = (Task)Field(window, "botTurnTask")!;
            GameState loaded = Fen.Parse("7k/8/8/8/8/8/P7/7K b - - 0 42");
            string before = loaded.ToFen();
            Call(window, "LoadGame", loaded);
            Finish(old);
            Assert.Same(loaded, Game(window));
            Assert.Equal(before, loaded.ToFen());
            Assert.Empty(loaded.History);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void LoadedBotTurnsAndRematchesStartAutomatically() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            GameState loaded = Fen.Parse("7k/P7/8/8/8/8/8/7K w - - 0 1");
            Call(window, "LoadGame", loaded);
            StartSession(window);
            FinishAutomatic(window);
            Assert.IsType<Queen>(loaded.Board[At("a8")]);
            Assert.Equal(Player.Black, loaded.CurrentPlayer);
            Call(window, "RestartGame");
            FinishAutomatic(window);
            Assert.Single(Game(window).History);
            Assert.Equal(Player.Black, Game(window).CurrentPlayer);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void BotRespondsToAHumansDrawOfferAndHumanCannotAcceptTheirOwnOffer() => fixture.Run(() =>
    {
        MainWindow window = new(Player.Black);
        try
        {
            GameState game = Fen.Parse("3q3k/8/8/3p4/8/8/8/K2R4 b - - 0 42");
            Call(window, "LoadGame", game);
            StartSession(window);
            Call(window, "OfferDraw_Click", window, new RoutedEventArgs());
            Assert.False(((Button)window.FindName("AcceptDrawButton")).IsEnabled);
            Call(window, "AcceptDraw_Click", window, new RoutedEventArgs());
            Assert.False(game.IsGameOver());
            Call(window, "HandleMove", FindMove(game, "h8", "h7"));
            FinishAutomatic(window);
            Assert.Equal(EndReason.DrawAgreement, game.Result!.Reason);
        }
        finally { window.Close(); }
    });

    private static Player? SelectedSide(StartWindow start) => (Player?)typeof(StartWindow)
        .GetProperty("SelectedHumanPlayer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(start);
    private static GameState Game(MainWindow window) => (GameState)Field(window, "gameState")!;
    private static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    private static object? Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void StartSession(MainWindow window)
    {
        typeof(MainWindow).GetField("sessionStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        Call(window, "UpdateStatus");
    }
    private static void FinishAutomatic(MainWindow window) => Finish((Task)Field(window, "botTurnTask")!);
    private static void Finish(Task task)
    {
        Task bounded = task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        DispatcherFrame frame = new();
        _ = bounded.ContinueWith(_ => frame.Dispatcher.BeginInvoke(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        bounded.GetAwaiter().GetResult();
    }
    private static void Escape(MainWindow window)
    {
        KeyEventArgs key = new(Keyboard.PrimaryDevice, new TestSource(), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(key);
    }
    private sealed class TestSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;
        public override bool IsDisposed => false;
        protected override CompositionTarget? GetCompositionTargetCore() => null;
    }
    private static void Preview(Window window, string variable, int width, int height)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        if (path == null) return;
        FrameworkElement root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        PngBitmapEncoder encoder = new(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using Stream output = File.Create(path); encoder.Save(output);
    }
}
