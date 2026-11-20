using System.Windows;
using ChessLogic;

namespace ChessUI;

public partial class StartWindow : Window
{
    public StartWindow() => InitializeComponent();

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (ColourPanel != null)
            ColourPanel.Visibility = BotModeRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    internal Player? SelectedHumanPlayer => BotModeRadio.IsChecked == true
        ? BlackRadio.IsChecked == true ? Player.Black : Player.White : null;

    private void StartGame_Click(object sender, RoutedEventArgs e)
    {
        MainWindow game = new(SelectedHumanPlayer);
        Application.Current.MainWindow = game;
        game.Show();
        Close();
    }
}
