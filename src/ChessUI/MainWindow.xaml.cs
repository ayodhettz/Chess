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
                    Piece piece = board[row, col];
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
            UpdateStatus();
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
            ClaimDrawButton.IsEnabled = gameState.CanClaimDraw();
            FenPositionButton.IsEnabled = MenuContainer.Content is not (PromotionMenu or FenMenu);

            while (HistoryList.Items.Count < gameState.History.Count)
            {
                int index = HistoryList.Items.Count;
                MoveRecord move = gameState.History[index];
                HistoryList.Items.Add($"{move.FullMoveNumber}. {move.Player}: {move}");
            }
            if (gameState.History.Count == 0) HistoryList.Items.Clear();
            if (HistoryList.Items.Count > 0) HistoryList.ScrollIntoView(HistoryList.Items[^1]);
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
