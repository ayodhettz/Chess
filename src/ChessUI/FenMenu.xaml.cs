using ChessLogic;
using System.Windows;
using System.Windows.Controls;

namespace ChessUI
{
    public partial class FenMenu : UserControl
    {
        public event Action<GameState>? PositionLoaded;
        public event Action? Closed;

        public FenMenu(string fen)
        {
            InitializeComponent();
            FenTextBox.Text = fen;
            Loaded += (_, _) => { FenTextBox.Focus(); FenTextBox.SelectAll(); };
        }

        public void Close() => Closed?.Invoke();

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if (!Fen.TryParse(FenTextBox.Text, out GameState? game, out string? error))
            {
                ErrorText.Text = error;
                return;
            }
            PositionLoaded?.Invoke(game);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
