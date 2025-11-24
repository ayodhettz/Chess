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
        private Position selectedPosition = null;
        public MainWindow()
        {
            InitializeComponent();
            InitializeBoard();

            gameState = new GameState(Board.Initial(), Player.White);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
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
            if(IsMenuOnScreen())
                return;


            Point point = e.GetPosition(BoardGrid);
            Position pos = ToSquarePosition(point);

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
            IEnumerable<Move> legalMoves = gameState.LegalMoves(pos);
            if(legalMoves.Any())
            {
                selectedPosition = pos;
                CacheMoves(legalMoves);
                HighlightMoves(legalMoves);
            }
        }

        private void OnToPositionSelected(Position pos)
        {
            selectedPosition = null;
            ClearHighlights();

            if (moveCache.TryGetValue(pos, out Move move))
            {
                HandleMove(move);
            }
        }
        private void HandleMove(Move move)
        {
            gameState.ApplyMove(move);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);

            if(gameState.IsGameOver())
            {
                ShowGameOverMenu();
            }
        }

        private void CacheMoves(IEnumerable<Move> moves)
        {
            moveCache.Clear();
            foreach(Move move in moves)
            {
                moveCache[move.To] = move;
            }
        }

        private void HighlightMoves(IEnumerable<Move> moves)
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
            ClearHighlights();
            moveCache.Clear();
            gameState = new GameState(Board.Initial(), Player.White);
            DrawBoard(gameState.Board);
            SetCursor(gameState.CurrentPlayer);
        }
    }
}