using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ChessLogic;
using Microsoft.Win32;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChessUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly Image[,] pieceImages = new Image[8, 8];
        private readonly Rectangle[,] checkSquares = new Rectangle[8, 8];
        private readonly Rectangle[,] highlights = new Rectangle[8, 8];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();

        private GameState gameState;
        private Position? selectedPosition;
        private CancellationTokenSource? botCancellation;
        private bool windowClosed;
        private readonly Player? humanPlayer;
        public MainWindow() : this(null) { }

        public MainWindow(Player? humanPlayer) : this(humanPlayer, null) { }

        public MainWindow(Player? humanPlayer, GameState? initialGame, BotDifficulty difficulty = BotDifficulty.Medium)
        {
            if (humanPlayer is not (null or Player.White or Player.Black)) throw new ArgumentOutOfRangeException(nameof(humanPlayer));
            _ = BotSearchLimits.For(difficulty);
            botDifficulty = difficulty;
            this.humanPlayer = humanPlayer;
            InitializeComponent();
            InitializeBoard();

            gameState = initialGame ?? new GameState(Board.Initial(), Player.White);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
            UpdateStatus();
            if (gameState.IsGameOver()) ShowGameOverMenu();
            Closed += (_, _) => { windowClosed = true; botCancellation?.Cancel(); };
            Loaded += (_, _) => { sessionStarted = true; UpdateStatus(); };
        }

        private void InitializeBoard()
        {
            BoardArtwork.FillSquares(SquareGrid);
            BoardArtwork.FillCoordinates(RankLabels, FileLabels, humanPlayer == Player.Black);
            for(int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Position square = DisplaySquare(row, col);
                    Image pieceImage = new Image { Margin = new Thickness(3.2) };
                    pieceImages[square.Row, square.Column] = pieceImage;
                    PieceGrid.Children.Add(pieceImage);

                    Rectangle check = new();
                    checkSquares[square.Row, square.Column] = check;
                    CheckGrid.Children.Add(check);
                    Rectangle highlight = new Rectangle();
                    highlights[square.Row, square.Column] = highlight;
                    HighlightGrid.Children.Add(highlight);
                }
            }
        }

        private void DrawBoard(Board board)
        {
            for(int row = 0; row < 8; row++)
            {
                for(int col = 0; col < 8; col++)
                {
                    Piece? piece = board[row, col];
                    pieceImages[row, col].Source = Images.GetImage(piece);
                }
            }
        }

        private void BoardGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!CanHumanAct() || e.ChangedButton != MouseButton.Left)
                return;


            Point point = e.GetPosition(BoardGrid);
            Position pos = ToSquarePosition(point);
            if (!Board.IsInside(pos)) return;

            if(selectedPosition == null)
            {
                OnFromPositionSelected(pos);
            }
            else
            {
                OnToPositionSelected(pos);
            }
        }

        private Position ToSquarePosition(Point point)
        {
            double squareWidth = BoardGrid.ActualWidth / 8;
            double squareHeight = BoardGrid.ActualHeight / 8;
            int col = (int)(point.X / squareWidth);
            int row = (int)(point.Y / squareHeight);
            return DisplaySquare(row, col);
        }

        private void OnFromPositionSelected(Position pos)
        {
            if (!CanHumanAct()) return;
            Move[] legalMoves = gameState.LegalMoves(pos).ToArray();
            if (legalMoves.Length > 0)
            {
                ClearHighlights();
                selectedPosition = pos;
                CacheMoves(legalMoves);
                HighlightMoves();
            }
        }

        private void OnToPositionSelected(Position pos)
        {
            selectedPosition = null;
            ClearHighlights();

            if (moveCache.TryGetValue(pos, out Move? move))
            {
                if(move.Type == MoveType.PawnPromotion)
                {
                    HandlePromotion(move.From, move.To);
                }
                else
                    HandleMove(move);
            }
            else
            {
                OnFromPositionSelected(pos);
            }
        }
        private void HandleMove(Move move)
        {
            if (!CanHumanAct()) return;

            if (ClaimWithMoveCheckBox.IsChecked == true)
            {
                DrawBoard(gameState.Board);
                if (!gameState.CanClaimDraw(move))
                {
                    ClaimHintText.Text = "That move does not qualify for a draw claim.";
                    UpdateStatus();
                    return;
                }
                gameState.ClaimDraw(move);
                UpdateStatus();
                ShowGameOverMenu();
                return;
            }

            gameState.ApplyMove(move);
            botPaused = false;
            BotStatusText.Visibility = Visibility.Collapsed;
            ClaimHintText.Text = "";
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
            UpdateStatus();

            if(gameState.IsGameOver())
            {
                ShowGameOverMenu();
            }
        }

        private void HandlePromotion(Position from, Position to)
        {
            pieceImages[to.Row, to.Column].Source = Images.GetImage(PieceType.Pawn, gameState.CurrentPlayer);
            pieceImages[from.Row, from.Column].Source = null;

            PromotionMenu promMenu = new PromotionMenu(gameState.CurrentPlayer);
            MenuContainer.Content = promMenu;
            UpdateStatus();

            promMenu.PieceSelected += type =>
            {
                MenuContainer.Content = null;
                Move promMove = new PawnPromotion(from, to, type);
                HandleMove(promMove);
            };
        }

        private void CacheMoves(IEnumerable<Move> moves)
        {
            moveCache.Clear();
            foreach(Move move in moves)
            {
                moveCache[move.To] = move;
            }
        }

        private void HighlightMoves()
        {
            Color colour = Color.FromArgb(150, 125, 255, 125);

            foreach(Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = new SolidColorBrush(colour);
            }
        }

        private void ClearHighlights()
        {
            foreach (Rectangle highlight in highlights)
            {
                highlight.Fill = Brushes.Transparent;
            }
        }

        private void SetCursor(Player player)
        {
            if(player == Player.White)
            {
                Cursor = ChessCursors.WhiteCursor;
            }
            else
            {
                Cursor = ChessCursors.BlackCursor;
            }
        }

        private bool IsMenuOnScreen()
        {
            return MenuContainer.Content != null;
        }

        private void ShowGameOverMenu()
        {
            GameOverMenu menu = new GameOverMenu(gameState, humanPlayer);
            MenuContainer.Content = menu;
            UpdateStatus();

            menu.OptionSelected += option =>
            {
                if (option == Options.Restart)
                {
                    MenuContainer.Content = null;
                    RestartGame();
                }
                else if (option == Options.Exit)
                {
                    Application.Current.Shutdown();
                }
            };
        }

        private void RestartGame()
        {
            LoadGame(new GameState(Board.Initial(), Player.White));
        }

        private void LoadGame(GameState game)
        {
            botCancellation?.Cancel();
            botPaused = false;
            selectedPosition = null;
            ClearHighlights();
            moveCache.Clear();
            HistoryList.Items.Clear();
            gameState = game;
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
            ClaimWithMoveCheckBox.IsChecked = false;
            ClaimHintText.Text = "";
            FileStatusText.Visibility = Visibility.Collapsed;
            BotStatusText.Visibility = Visibility.Collapsed;
            UpdateStatus();
        }

        private void SaveGame_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessFiles()) return;
            SaveFileDialog dialog = new() { Filter = "Chess games (*.chess.json)|*.chess.json", DefaultExt = ".chess.json", AddExtension = true };
            if (dialog.ShowDialog(this) == true) SaveGameToFile(dialog.FileName);
        }

        private void LoadGame_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessFiles()) return;
            OpenFileDialog dialog = new() { Filter = "Chess games (*.chess.json)|*.chess.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) LoadGameFromFile(dialog.FileName);
        }

        private void SaveGameToFile(string path)
        {
            if (!CanAccessFiles()) return;
            try { GameFiles.Save(path, gameState); SetFileStatus("Game saved."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not save game: {error.Message}"); }
        }

        private void LoadGameFromFile(string path)
        {
            if (!CanAccessFiles()) return;
            try
            {
                GameState loaded = GameFiles.Load(path);
                MenuContainer.Content = null;
                LoadGame(loaded);
                if (loaded.IsGameOver()) ShowGameOverMenu();
                SetFileStatus("Game loaded.");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not load game: {error.Message}"); }
        }

        private void SetFileStatus(string message)
        {
            FileStatusText.Text = message;
            FileStatusText.Visibility = Visibility.Visible;
        }

        private void ImportPgn_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessFiles()) return;
            OpenFileDialog dialog = new() { Filter = "PGN games (*.pgn)|*.pgn", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) ImportPgnFromFile(dialog.FileName);
        }

        private void ExportPgn_Click(object sender, RoutedEventArgs e)
        {
            if (!CanAccessFiles()) return;
            SaveFileDialog dialog = new() { Filter = "PGN games (*.pgn)|*.pgn", DefaultExt = ".pgn", AddExtension = true };
            if (dialog.ShowDialog(this) == true) ExportPgnToFile(dialog.FileName);
        }

        private void ImportPgnFromFile(string path)
        {
            if (!CanAccessFiles()) return;
            try
            {
                GameState loaded = Pgn.Load(path);
                MenuContainer.Content = null;
                LoadGame(loaded);
                if (loaded.IsGameOver()) ShowGameOverMenu();
                SetFileStatus("PGN loaded (main line).");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not import PGN: {error.Message}"); }
        }

        private void ExportPgnToFile(string path)
        {
            if (!CanAccessFiles()) return;
            try { Pgn.Save(path, gameState); SetFileStatus("PGN exported (main line)."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not export PGN: {error.Message}"); }
        }

        private void FenPosition_Click(object sender, RoutedEventArgs e)
        {
            if (botCancellation != null || MenuContainer.Content is PromotionMenu or FenMenu) return;
            object previousMenu = MenuContainer.Content;
            FenMenu menu = new(gameState.ToFen());
            MenuContainer.Content = menu;
            menu.Closed += () =>
            {
                MenuContainer.Content = previousMenu;
                UpdateStatus();
            };
            menu.PositionLoaded += game =>
            {
                MenuContainer.Content = null;
                LoadGame(game);
                if (game.IsGameOver()) ShowGameOverMenu();
            };
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            Position? checkedKing = gameState.CheckedKingSquare;
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                    checkSquares[row, col].Fill = checkedKing == new Position(row, col)
                        ? new SolidColorBrush(Color.FromArgb(190, 190, 48, 40)) : Brushes.Transparent;
            MatchTitleText.Text = humanPlayer == null ? "Local two-player" : "You vs Computer";
            OpponentText.Text = humanPlayer == null ? "Black" : "Computer";
            HumanText.Text = humanPlayer == null ? "White" : "You";
            OpponentColourText.Text = (humanPlayer?.Opponent() ?? Player.Black).ToString();
            HumanColourText.Text = (humanPlayer ?? Player.White).ToString();
            StatusText.Text = gameState.IsGameOver() ? "Game over" :
                $"{gameState.CurrentPlayer} to move" +
                (gameState.Board.IsInCheck(gameState.CurrentPlayer) ? " — check" : "");
            ActionPanel.IsEnabled = CanHumanAct();
            BotMoveMenuItem.IsEnabled = CanHumanAct();
            SuggestMoveButton.IsEnabled = CanHumanAct();
            CancelBotMenuItem.IsEnabled = botCancellation != null;
            ResumeBotButton.Visibility = IsBotTurn && botPaused && !IsMenuOnScreen() ? Visibility.Visible : Visibility.Collapsed;
            ResumeBotButton.IsEnabled = botCancellation == null;
            bool claimNow = !IsBotTurn && gameState.CanClaimDraw();
            bool claimWithMove = !IsBotTurn && !claimNow && gameState.CanClaimDrawWithIntendedMove();
            ClaimDrawButton.Visibility = claimNow ? Visibility.Visible : Visibility.Collapsed;
            ClaimDrawButton.IsEnabled = claimNow;
            ClaimWithMoveCheckBox.Visibility = claimWithMove ? Visibility.Visible : Visibility.Collapsed;
            ClaimHintText.Visibility = (claimNow || claimWithMove) && !string.IsNullOrEmpty(ClaimHintText.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            bool pendingOffer = gameState.DrawOfferedBy != null;
            OfferDrawButton.Visibility = !pendingOffer && !gameState.IsGameOver() ? Visibility.Visible : Visibility.Collapsed;
            OfferDrawButton.IsEnabled = gameState.CanOfferDraw;
            OfferDrawButton.ToolTip = gameState.FullMoveNumber < 2 ? "Available after both players have made a move." : "Offer a draw to your opponent.";
            DrawOfferPanel.Visibility = pendingOffer ? Visibility.Visible : Visibility.Collapsed;
            if (gameState.DrawOfferedBy is Player offeredBy)
            {
                DrawOfferText.Text = $"{offeredBy} offers a draw.\n{offeredBy.Opponent()}: accept or decline.";
                if (humanPlayer == offeredBy) DrawOfferText.Text = "You offered a draw. Play your move to let the bot respond.";
                AcceptDrawButton.IsEnabled = DeclineDrawButton.IsEnabled = humanPlayer == null || humanPlayer == offeredBy.Opponent();
            }
            FenPositionButton.IsEnabled = botCancellation == null && MenuContainer.Content is not (PromotionMenu or FenMenu);
            UndoButton.IsEnabled = CanNavigateHistory() && gameState.CanUndo;
            RedoButton.IsEnabled = CanNavigateHistory() && gameState.CanRedo;
            SaveGameMenuItem.IsEnabled = CanAccessFiles();
            LoadGameMenuItem.IsEnabled = CanAccessFiles();
            ImportPgnMenuItem.IsEnabled = CanAccessFiles();
            ExportPgnMenuItem.IsEnabled = CanAccessFiles();
            CommandManager.InvalidateRequerySuggested();

            HistoryList.Items.Clear();
            foreach (var turn in gameState.History.GroupBy(move => move.FullMoveNumber))
            {
                HistoryList.Items.Add(new ScoresheetEntry(turn.Key.ToString(),
                    turn.FirstOrDefault(move => move.Player == Player.White)?.San ?? "",
                    turn.FirstOrDefault(move => move.Player == Player.Black)?.San ?? ""));
            }
            if (HistoryList.Items.Count > 0) HistoryList.ScrollIntoView(HistoryList.Items[^1]);
            QueueBotTurn();
        }

        private bool CanNavigateHistory()
        {
            return MenuContainer.Content is null or PauseMenu or GameOverMenu;
        }

        private bool CanAccessFiles() => botCancellation == null && CanNavigateHistory();

        private void NavigateHistory(bool redo)
        {
            if (!CanNavigateHistory()) return;
            if (!(redo ? gameState.CanRedo : gameState.CanUndo)) return;
            botPaused = true;
            botCancellation?.Cancel();
            bool changed = redo ? gameState.Redo() : gameState.Undo();
            if (!changed) return;
            // A bot match takes back or restores a complete human/bot round.
            while (humanPlayer != null && gameState.CurrentPlayer != humanPlayer && !gameState.IsGameOver() &&
                (redo ? gameState.CanRedo : gameState.CanUndo))
            {
                if (redo) gameState.Redo(); else gameState.Undo();
            }

            MenuContainer.Content = null;
            selectedPosition = null;
            ClearHighlights();
            moveCache.Clear();
            ClaimWithMoveCheckBox.IsChecked = false;
            ClaimHintText.Text = "";
            BotStatusText.Visibility = Visibility.Collapsed;
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
            UpdateStatus();
            if (gameState.IsGameOver()) ShowGameOverMenu();
        }

        private void Undo_Click(object sender, RoutedEventArgs e) => NavigateHistory(false);
        private void Redo_Click(object sender, RoutedEventArgs e) => NavigateHistory(true);

        private void Undo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            NavigateHistory(false);
            e.Handled = true;
        }

        private void Redo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            NavigateHistory(true);
            e.Handled = true;
        }

        private void Undo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanNavigateHistory() && gameState.CanUndo;
            e.Handled = true;
        }

        private void Redo_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanNavigateHistory() && gameState.CanRedo;
            e.Handled = true;
        }

        private void ClaimDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!CanHumanAct() || !gameState.CanClaimDraw()) return;
            gameState.ClaimDraw();
            ClearHighlights();
            selectedPosition = null;
            ShowGameOverMenu();
        }

        private void Resign_Click(object sender, RoutedEventArgs e)
        {
            if (!CanHumanAct()) return;
            gameState.Resign();
            ClearHighlights();
            selectedPosition = null;
            ShowGameOverMenu();
        }

        private void OfferDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!CanHumanAct() || !gameState.CanOfferDraw) return;
            gameState.OfferDraw();
            UpdateStatus();
        }

        private void AcceptDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!CanHumanAct() || gameState.DrawOfferedBy is not Player offeredBy || humanPlayer == offeredBy) return;
            gameState.AcceptDraw(offeredBy.Opponent());
            selectedPosition = null;
            ClearHighlights();
            ShowGameOverMenu();
        }

        private void DeclineDraw_Click(object sender, RoutedEventArgs e)
        {
            if (!CanHumanAct() || gameState.DrawOfferedBy is not Player offeredBy || humanPlayer == offeredBy) return;
            gameState.DeclineDraw(offeredBy.Opponent());
            UpdateStatus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            if (botCancellation != null)
            {
                botCancellation.Cancel();
                if (humanPlayer == null || !IsBotTurn) return;
                botPaused = true;
            }

            if (MenuContainer.Content is FenMenu fenMenu)
            {
                fenMenu.Close();
                return;
            }

            if (MenuContainer.Content is PauseMenu)
            {
                MenuContainer.Content = null;
                botPaused = false;
                UpdateStatus();
                return;
            }

            // Promotion and game-over menus must stay until resolved.
            if (!IsMenuOnScreen() && !gameState.IsGameOver())
            {
                PauseMenu pauseMenu = new PauseMenu();
                MenuContainer.Content = pauseMenu;
                pauseMenu.OptionSelected += option =>
                {
                    if (option == Options.Continue)
                    {
                        MenuContainer.Content = null;
                        botPaused = false;
                        UpdateStatus();
                    }
                    else if (option == Options.Restart)
                    {
                        MenuContainer.Content = null;
                        RestartGame();
                    }
                };
                UpdateStatus();
            }
        }
    }
}
