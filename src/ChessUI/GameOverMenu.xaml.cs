using ChessLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChessUI
{
    /// <summary>
    /// Interaction logic for GameOverMenu.xaml
    /// </summary>
    public partial class GameOverMenu : UserControl
    {
        public event Action<Options>? OptionSelected;

        public GameOverMenu(GameState gameState, Player? perspective = null)
        {
            InitializeComponent();

            Result result = gameState.Result ?? throw new ArgumentException("The game has not ended.", nameof(gameState));
            bool draw = result.Winner == Player.None;
            bool won = !draw && (perspective == null || result.Winner == perspective);
            Winner_Text.Text = perspective == null || draw ? GetWinnerText(result.Winner) : won ? "You win" : "You lose";
            Loaded += (_, _) => ResultMotion.Show(ResultPanel, won, draw);
            Reason_Text.Text = GetReasonText(result.Reason, result.ResignedBy ?? gameState.CurrentPlayer);
        }

        private static string GetWinnerText(Player winner)
        {
            return winner switch
            {
                Player.White => "White Wins!",
                Player.Black => "Black Wins!",
                _ => "It's a Draw!",
            };
        }

        private static string PlayerString(Player player)
        {
            return player switch
            {
                Player.White => "White",
                Player.Black => "Black",
                _ => ""
            };
        }

        private static string GetReasonText(EndReason reason, Player currPlayer)
        {
            return reason switch
            {
                EndReason.Checkmate => $"CHECKMATE - {PlayerString(currPlayer)} cannot move",
                EndReason.Stalemate => "The game ended in a stalemate.",
                EndReason.Resignation => $"{PlayerString(currPlayer)}  has resigned.",
                EndReason.InsufficientMaterial => "The game ended due to insufficient material.",
                EndReason.FiftyMoveRule => "The game ended due to the fifty-move rule.",
                EndReason.ThreefoldRepetition => "The game ended due to threefold repetition.",
                EndReason.FivefoldRepetition => "The game ended due to fivefold repetition.",
                EndReason.SeventyFiveMoveRule => "The game ended due to the seventy-five-move rule.",
                EndReason.RecordedResult => "Game result recorded in the imported PGN.",
                EndReason.DrawAgreement => "Both players agreed to a draw.",
                _ => ""
            };
        }
        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            OptionSelected?.Invoke(Options.Exit);
        }

        private void Rematch_Button_Click(object sender, RoutedEventArgs e)
        {
            OptionSelected?.Invoke(Options.Restart);
        }
    }
}
