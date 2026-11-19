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

namespace ChessUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly Image[,] pieceImages = new Image[8, 8];
        private readonly Rectangle[,] highlights = new Rectangle[8, 8];
        private readonly Dictionary<Position, Move> moveCache = new Dictionary<Position, Move>();

        private GameState gameState;
        private Position? selectedPosition;
        public MainWindow()
        {
            InitializeComponent();
            InitializeBoard();

            gameState = new GameState(Board.Initial(), Player.White);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
            UpdateStatus();
        }

        private void InitializeBoard()
        {
            for(int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Image pieceImage = new Image();
                    pieceImages[row, col] = pieceImage;
                    PieceGrid.Children.Add(pieceImage);

                    Rectangle highlight = new Rectangle();
                    highlights[row, col] = highlight;
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
            if (IsMenuOnScreen() || gameState.IsGameOver() || e.ChangedButton != MouseButton.Left)
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
            return new Position(row, col);
        }

        private void OnFromPositionSelected(Position pos)
        {
            Move[] legalMoves = gameState.LegalMoves(pos).ToArray();
            if (legalMoves.Length > 0)
            {
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
            if (gameState.IsGameOver()) return;

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
            foreach (Position to in moveCache.Keys)
            {
                highlights[to.Row, to.Column].Fill = Brushes.Transparent;
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
            GameOverMenu menu = new GameOverMenu(gameState);
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
            UpdateStatus();
        }

        private void SaveGame_Click(object sender, RoutedEventArgs e)
        {
            if (!CanNavigateHistory()) return;
            SaveFileDialog dialog = new() { Filter = "Chess games (*.chess.json)|*.chess.json", DefaultExt = ".chess.json", AddExtension = true };
            if (dialog.ShowDialog(this) == true) SaveGameToFile(dialog.FileName);
        }

        private void LoadGame_Click(object sender, RoutedEventArgs e)
        {
            if (!CanNavigateHistory()) return;
            OpenFileDialog dialog = new() { Filter = "Chess games (*.chess.json)|*.chess.json", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) LoadGameFromFile(dialog.FileName);
        }

        private void SaveGameToFile(string path)
        {
            if (!CanNavigateHistory()) return;
            try { GameFiles.Save(path, gameState); SetFileStatus("Game saved."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not save game: {error.Message}"); }
        }

        private void LoadGameFromFile(string path)
        {
            if (!CanNavigateHistory()) return;
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
            if (!CanNavigateHistory()) return;
            OpenFileDialog dialog = new() { Filter = "PGN games (*.pgn)|*.pgn", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) ImportPgnFromFile(dialog.FileName);
        }

        private void ExportPgn_Click(object sender, RoutedEventArgs e)
        {
            if (!CanNavigateHistory()) return;
            SaveFileDialog dialog = new() { Filter = "PGN games (*.pgn)|*.pgn", DefaultExt = ".pgn", AddExtension = true };
            if (dialog.ShowDialog(this) == true) ExportPgnToFile(dialog.FileName);
        }

        private void ImportPgnFromFile(string path)
        {
            if (!CanNavigateHistory()) return;
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
            if (!CanNavigateHistory()) return;
            try { Pgn.Save(path, gameState); SetFileStatus("PGN exported (main line)."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
            { SetFileStatus($"Could not export PGN: {error.Message}"); }
        }

        private void FenPosition_Click(object sender, RoutedEventArgs e)
        {
            if (MenuContainer.Content is PromotionMenu or FenMenu) return;
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
            StatusText.Text = gameState.IsGameOver() ? "Game over" :
                $"{gameState.CurrentPlayer} to move" + (gameState.Board.IsInCheck(gameState.CurrentPlayer) ? " — check" : "");
            ActionPanel.IsEnabled = !IsMenuOnScreen() && !gameState.IsGameOver();
            bool claimNow = gameState.CanClaimDraw();
            bool claimWithMove = !claimNow && gameState.CanClaimDrawWithIntendedMove();
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
            }
            FenPositionButton.IsEnabled = MenuContainer.Content is not (PromotionMenu or FenMenu);
            UndoButton.IsEnabled = CanNavigateHistory() && gameState.CanUndo;
            RedoButton.IsEnabled = CanNavigateHistory() && gameState.CanRedo;
            SaveGameMenuItem.IsEnabled = CanNavigateHistory();
            LoadGameMenuItem.IsEnabled = CanNavigateHistory();
            ImportPgnMenuItem.IsEnabled = CanNavigateHistory();
            ExportPgnMenuItem.IsEnabled = CanNavigateHistory();
            CommandManager.InvalidateRequerySuggested();

            while (HistoryList.Items.Count > gameState.History.Count)
            {
                HistoryList.Items.RemoveAt(HistoryList.Items.Count - 1);
            }

            while (HistoryList.Items.Count < gameState.History.Count)
            {
                int index = HistoryList.Items.Count;
                MoveRecord move = gameState.History[index];
                HistoryList.Items.Add($"{move.FullMoveNumber}. {move.Player}: {move.San}");
            }
            if (HistoryList.Items.Count > 0) HistoryList.ScrollIntoView(HistoryList.Items[^1]);
        }

        private bool CanNavigateHistory()
        {
            return MenuContainer.Content is null or PauseMenu or GameOverMenu;
        }

        private void NavigateHistory(bool redo)
        {
            if (!CanNavigateHistory()) return;
            bool changed = redo ? gameState.Redo() : gameState.Undo();
            if (!changed) return;

            MenuContainer.Content = null;
            selectedPosition = null;
            ClearHighlights();
            moveCache.Clear();
            ClaimWithMoveCheckBox.IsChecked = false;
            ClaimHintText.Text = "";
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
            if (IsMenuOnScreen() || !gameState.CanClaimDraw()) return;
            gameState.ClaimDraw();
            ClearHighlights();
            selectedPosition = null;
            ShowGameOverMenu();
        }

        private void Resign_Click(object sender, RoutedEventArgs e)
        {
            if (IsMenuOnScreen() || gameState.IsGameOver()) return;
            gameState.Resign();
            ClearHighlights();
            selectedPosition = null;
            ShowGameOverMenu();
        }

        private void OfferDraw_Click(object sender, RoutedEventArgs e)
        {
            if (IsMenuOnScreen() || !gameState.CanOfferDraw) return;
            gameState.OfferDraw();
            UpdateStatus();
        }

        private void AcceptDraw_Click(object sender, RoutedEventArgs e)
        {
            if (IsMenuOnScreen() || gameState.DrawOfferedBy is not Player offeredBy) return;
            gameState.AcceptDraw(offeredBy.Opponent());
            selectedPosition = null;
            ClearHighlights();
            ShowGameOverMenu();
        }

        private void DeclineDraw_Click(object sender, RoutedEventArgs e)
        {
            if (IsMenuOnScreen() || gameState.DrawOfferedBy is not Player offeredBy) return;
            gameState.DeclineDraw(offeredBy.Opponent());
            UpdateStatus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;

            if (MenuContainer.Content is FenMenu fenMenu)
            {
                fenMenu.Close();
                return;
            }

            if (MenuContainer.Content is PauseMenu)
            {
                MenuContainer.Content = null;
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
