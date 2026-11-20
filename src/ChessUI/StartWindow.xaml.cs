using System.IO;
using System.Windows;
using System.Windows.Controls;
using ChessLogic;
using Microsoft.Win32;

namespace ChessUI;

public partial class StartWindow : Window
{
    public StartWindow()
    {
        InitializeComponent();
        BoardArtwork.FillSquares(PreviewSquares);
        BoardArtwork.FillCoordinates(PreviewRanks, PreviewFiles, false);
        Board board = Board.Initial();
        for (int row = 0; row < 8; row++)
            for (int column = 0; column < 8; column++)
                PreviewPieces.Children.Add(new Image { Source = Images.GetImage(board[row, column]), Margin = new Thickness(3.2) });
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (ColourPanel == null) return;
        ColourPanel.Visibility = BotModeRadio.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateStartLabel();
    }

    private void Colour_Changed(object sender, RoutedEventArgs e) => UpdateStartLabel();

    private void UpdateStartLabel()
    {
        if (StartGameButton != null)
            StartGameButton.Content = SelectedHumanPlayer switch
            {
                Player.White => "Take White & play", Player.Black => "Take Black & play", _ => "Start local game"
            };
    }

    internal Player? SelectedHumanPlayer => BotModeRadio.IsChecked == true
        ? BlackRadio.IsChecked == true ? Player.Black : Player.White : null;

    private void Online_Click(object sender, RoutedEventArgs e)
    {
        OnlineWindow window = new(); Application.Current.MainWindow = window; window.Show(); Close();
    }

    private void StartGame_Click(object sender, RoutedEventArgs e) => OpenMatch(null);

    private void OpenMatch(GameState? game)
    {
        MainWindow window = new(SelectedHumanPlayer, game);
        Application.Current.MainWindow = window;
        window.Show();
        Close();
    }

    private void OpenGame_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new() { Filter = "Chess games (*.chess.json)|*.chess.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) OpenSavedGame(dialog.FileName);
    }

    private void OpenSavedGame(string path)
    {
        try { OpenMatch(GameFiles.Load(path)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            LoadErrorText.Text = $"Could not load game: {error.Message}";
            LoadErrorText.Visibility = Visibility.Visible;
        }
    }
}
