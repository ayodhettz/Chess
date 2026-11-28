using System.Windows;
using System.Windows.Media;
using ChessLogic;

namespace ChessUI;

public partial class MainWindow
{
    private readonly BotDifficulty botDifficulty;
    private bool sessionStarted;
    private bool botPaused;
    private Task botTurnTask = Task.CompletedTask;

    private bool IsBotTurn => humanPlayer != null && gameState.CurrentPlayer != humanPlayer && !gameState.IsGameOver();
    private bool CanHumanAct() => botCancellation == null && !IsMenuOnScreen() && !gameState.IsGameOver() && !IsBotTurn;
    private Position DisplaySquare(int row, int column) => humanPlayer == Player.Black
        ? new Position(7 - row, 7 - column) : new Position(row, column);

    private void QueueBotTurn()
    {
        if (sessionStarted && !windowClosed && IsBotTurn && !botPaused && !IsMenuOnScreen() && botCancellation == null)
            botTurnTask = PlayBotMoveAsync();
    }

    private async void SuggestMove_Click(object sender, RoutedEventArgs e) => await SuggestMoveAsync();
    private Task SuggestMoveAsync() => CanHumanAct() ? RunSearchAsync(suggestOnly: true) : Task.CompletedTask;

    private Task PlayBotMoveAsync()
    {
        if (botCancellation != null || IsMenuOnScreen() || gameState.IsGameOver() || humanPlayer != null && !IsBotTurn)
            return Task.CompletedTask;
        return RunSearchAsync(suggestOnly: false);
    }

    private void CancelBot_Click(object sender, RoutedEventArgs e)
    {
        if (IsBotTurn) botPaused = true;
        botCancellation?.Cancel();
        UpdateStatus();
    }

    private void ResumeBot_Click(object sender, RoutedEventArgs e)
    {
        botPaused = false;
        UpdateStatus();
    }

    private async Task RunSearchAsync(bool suggestOnly)
    {
        GameState original = gameState;
        using CancellationTokenSource cancellation = new();
        botCancellation = cancellation;
        selectedPosition = null;
        ClearHighlights();
        moveCache.Clear();
        BotStatusText.Text = suggestOnly ? "Finding a suggestion…" : "Bot is thinking…";
        BotStatusText.Visibility = Visibility.Visible;
        UpdateStatus();
        try
        {
            BotDecision decision = await new ChessBot().ChooseMoveAsync(original, suggestOnly ? BotDifficulty.Medium : botDifficulty, cancellation.Token);
            // Cancelled or replaced games must never receive an old result.
            if (windowClosed || cancellation.IsCancellationRequested || gameState != original) return;
            if (suggestOnly) ShowSuggestion(decision);
            else
            {
                ApplyBotDecision(decision);
                BotStatusText.Text = humanPlayer == null
                    ? $"Bot searched {decision.Nodes:N0} nodes; completed depth {decision.Depth}."
                    : gameState.IsGameOver() ? "Match finished." : "Bot has moved. Your turn.";
            }
        }
        catch (OperationCanceledException)
        {
            if (!windowClosed && gameState == original) BotStatusText.Text = "Search cancelled. Position unchanged.";
        }
        catch (Exception error)
        {
            if (gameState == original && IsBotTurn) botPaused = true;
            if (!windowClosed && gameState == original) BotStatusText.Text = $"Search could not finish: {error.Message}";
        }
        finally
        {
            botCancellation = null;
            if (!windowClosed) UpdateStatus();
        }
    }

    private void ShowSuggestion(BotDecision decision)
    {
        if (decision.AcceptDraw) BotStatusText.Text = "Suggestion: accept the draw.";
        else if (decision.ClaimDraw)
            BotStatusText.Text = decision.Move == null ? "Suggestion: claim a draw."
                : $"Suggestion: claim a draw with {San.Format(gameState, decision.Move)}.";
        else if (decision.Move != null) BotStatusText.Text = $"Suggestion: {San.Format(gameState, decision.Move)}";
        if (decision.Move is Move move)
        {
            highlights[move.From.Row, move.From.Column].Fill = new SolidColorBrush(Color.FromArgb(155, 244, 199, 100));
            highlights[move.To.Row, move.To.Column].Fill = new SolidColorBrush(Color.FromArgb(155, 244, 199, 100));
        }
    }

    private void ApplyBotDecision(BotDecision decision)
    {
        ClaimWithMoveCheckBox.IsChecked = false;
        ClaimHintText.Text = "";
        if (decision.AcceptDraw) gameState.AcceptDraw(gameState.CurrentPlayer);
        else if (decision.ClaimDraw) gameState.ClaimDraw(decision.Move);
        else if (decision.Move != null) gameState.ApplyMove(decision.Move);
        DrawBoard(gameState.Board);
        SetCursor(gameState.CurrentPlayer);
        if (gameState.IsGameOver()) ShowGameOverMenu();
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        StartWindow start = new();
        Application.Current.MainWindow = start;
        start.Show();
        Close();
    }
}
