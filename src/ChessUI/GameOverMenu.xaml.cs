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
        public event Action<Options> OptionSelected;

        public GameOverMenu(GameState gameState)
        {
            InitializeComponent();

            Result result = gameState.Result;
            Winner_Text.Text = GetWinnerText(result.Winner);
            Reason_Text.Text = GetReasonText(result.Reason, gameState.CurrentPlayer);
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
