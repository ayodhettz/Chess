using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Chess.Client;
using Chess.Contracts;
using ChessLogic;

namespace ChessUI;

public partial class OnlineWindow : Window
{
    private OnlineSession? session;
    private readonly Button[] buttons = new Button[64];
    private string? selected;
    private long revision = -1;
    private Seat? orientation;
    private bool closed;

    public OnlineWindow()
    {
        InitializeComponent();
        for (int index = 0; index < buttons.Length; index++)
        {
            Button button = new() { Style = (Style)FindResource("ButtonStyle"), Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = (Brush)FindResource((index / 8 + index % 8) % 2 == 0 ? "LightSquareColor" : "DarkSquareColor") };
            button.Click += Square_Click; button.PreviewKeyDown += Square_KeyDown;
            buttons[index] = button; BoardButtons.Children.Add(button);
        }
        Closing += OnClosing;
        Closed += async (_, _) => { closed = true; if (session != null) await session.DisposeAsync(); };
    }

    private async Task Run(Func<Task> action)
    {
        try { Notice.Text = ""; await action(); }
        catch (Exception error)
        {
            string message = session?.Error ?? (error is ArgumentException ? error.Message : "Could not connect. Check the server address and try again.");
            if (session is { Ticket: null }) { await session.DisposeAsync(); session = null; }
            Notice.Text = message;
        }
        if (!closed) Render();
    }

    private void PrepareSession()
    {
        if (session != null) return;
        session = new OnlineSession(new Uri(ServerAddress.Text.Trim(), UriKind.Absolute));
        session.Changed += () => { if (!closed && !Dispatcher.HasShutdownStarted) _ = Dispatcher.InvokeAsync(Render); };
    }

    private async void Create_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        PrepareSession();
        await session!.CreateAsync(PreferWhite.IsChecked == true ? Seat.White : Seat.Black);
    });

    private async void FindMatch_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        PrepareSession(); await session!.FindMatchAsync();
    });
    private void CancelSearch_Click(object sender, RoutedEventArgs e) => session?.CancelSearch();

    private async void Join_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        if (session == null && Uri.TryCreate(RoomInput.Text.Trim(), UriKind.Absolute, out Uri? link))
            ServerAddress.Text = link.GetLeftPart(UriPartial.Authority);
        PrepareSession(); await session!.JoinAsync(RoomInput.Text);
    });

    private async void Reconnect_Click(object sender, RoutedEventArgs e) => await Run(() => session!.ResumeAsync());
    private async void Offer_Click(object sender, RoutedEventArgs e) => await Send(OnlineAction.OfferDraw);
    private async void Accept_Click(object sender, RoutedEventArgs e) => await Send(OnlineAction.AcceptDraw);
    private async void Decline_Click(object sender, RoutedEventArgs e) => await Send(OnlineAction.DeclineDraw);
    private async void Claim_Click(object sender, RoutedEventArgs e) => await Send(OnlineAction.ClaimDraw);
    private Task Send(OnlineAction action, MoveIntent? move = null) => Run(() => session!.SendAsync(action, move));
    private async void Resign_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Your opponent will win. Resign this game?", "Resign", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await Send(OnlineAction.Resign);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(session!.Invitation); Notice.Text = "Invitation copied. It contains no seat credential."; }
        catch (System.Runtime.InteropServices.COMException) { Notice.Text = "Clipboard unavailable. Share the server address and room code shown here."; }
    }

    private void ClaimMode_Changed(object sender, RoutedEventArgs e) { selected = null; if (session?.State != null) Render(); }
    private MoveIntent[] Choices => session?.State is RoomSnapshot state ? ClaimIntended.IsChecked == true ? state.Draw.ClaimMoves : state.LegalMoves : [];
    private bool Live => session is { Ready: true, Busy: false, Pending: null, State.Status: RoomStatus.Active };
    private bool CanMove => Live && session!.State!.Turn == session.Ticket!.Seat &&
        (ClaimIntended.IsChecked == true || session.State.WhiteConnected && session.State.BlackConnected);

    private async void Square_Click(object sender, RoutedEventArgs e)
    {
        if (!CanMove || sender is not Button { Tag: string square }) return;
        MoveIntent[] choices = Choices.Where(move => move.From == selected && move.To == square).ToArray();
        if (choices.Length == 0) { selected = Choices.Any(move => move.From == square) ? square : null; Render(); return; }
        long expected = session!.State!.Revision;
        MoveIntent? chosen = choices.Length == 1 ? choices[0] : ChoosePromotion(choices);
        selected = null;
        if (chosen == null) { Render(); return; }
        if (!CanMove || session.State.Revision != expected) { Notice.Text = "The game changed. Choose your move again."; Render(); return; }
        await Send(ClaimIntended.IsChecked == true ? OnlineAction.ClaimDraw : OnlineAction.Move, chosen);
    }

    private MoveIntent? ChoosePromotion(MoveIntent[] choices)
    {
        Window dialog = new() { Owner = this, Title = "Choose promotion", Width = 390, Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("WindowColor"), ResizeMode = ResizeMode.NoResize };
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(16) };
        MoveIntent? selectedMove = null;
        foreach (MoveIntent choice in choices)
        {
            PieceType type = Enum.Parse<PieceType>(choice.Promotion!.Value.ToString());
            Button option = new() { Content = new Image { Source = Images.GetImage(type, session!.Ticket!.Seat == Seat.White ? Player.White : Player.Black) }, Width = 80, Height = 80 };
            AutomationProperties.SetName(option, type.ToString());
            option.Click += (_, _) => { selectedMove = choice; dialog.DialogResult = true; };
            panel.Children.Add(option);
        }
        dialog.Content = panel; dialog.ShowDialog(); return selectedMove;
    }

    private void Square_KeyDown(object sender, KeyEventArgs e)
    {
        int index = Array.IndexOf(buttons, (Button)sender), col = index % 8;
        int next = e.Key switch { Key.Up => index - 8, Key.Down => index + 8, Key.Left => col > 0 ? index - 1 : index,
            Key.Right => col < 7 ? index + 1 : index, Key.Home => index - col, Key.End => index - col + 7, _ => -1 };
        if (next >= 0 && next < 64) { buttons[next].Focus(); e.Handled = true; }
    }

    private void Render()
    {
        if (closed) return;
        FindMatch.IsEnabled = session?.Busy != true;
        CancelSearch.Visibility = SearchStatus.Visibility = session?.Searching == true ? Visibility.Visible : Visibility.Collapsed;
        CreateRoom.IsEnabled = JoinRoom.IsEnabled = session?.Busy != true;
        ServerAddress.IsEnabled = RoomInput.IsEnabled = session == null;
        if (session?.Error is string error) Notice.Text = error;
        RoomSnapshot? state = session?.State; SeatTicket? ticket = session?.Ticket;
        if (state == null || ticket == null) return;
        Lobby.Visibility = Visibility.Collapsed; MatchPanel.Visibility = Visibility.Visible;
        if (state.Revision != revision) { selected = null; revision = state.Revision; }
        if (orientation != ticket.Seat)
        {
            orientation = ticket.Seat; Ranks.Children.Clear(); Files.Children.Clear();
            BoardArtwork.FillCoordinates(Ranks, Files, ticket.Seat == Seat.Black);
        }
        GameState position = Fen.Parse(state.Fen);
        for (int index = 0; index < 64; index++)
        {
            int logical = ticket.Seat == Seat.Black ? 63 - index : index;
            Position square = new(logical / 8, logical % 8);
            string name = $"{(char)('a' + square.Column)}{8 - square.Row}";
            Piece? piece = position.Board[square];
            Button button = buttons[index]; button.Tag = name;
            button.Content = new Image { Source = Images.GetImage(piece), Margin = new Thickness(3.2) };
            bool legal = CanMove && Choices.Any(move => move.From == selected && move.To == name);
            button.BorderThickness = new Thickness(selected == name || legal ? 3 : 0);
            button.BorderBrush = (Brush)FindResource("AccentColor");
            AutomationProperties.SetName(button, $"{name}, {(piece == null ? "empty" : $"{piece.Colour} {piece.Type}")}");
        }
        bool ownTurn = state.Turn == ticket.Seat;
        bool opponentConnected = ticket.Seat == Seat.White ? state.BlackConnected : state.WhiteConnected;
        Players.Text = $"You: {ticket.Seat} · Opponent: {(opponentConnected ? "online" : "offline")}";
        Turn.Text = state.Status == RoomStatus.Closed ? "Room closed" : state.Result is GameOutcome result
            ? $"{(result.Winner == null ? "Draw" : $"{result.Winner} wins")} · {result.Reason}"
            : state.Status == RoomStatus.Waiting ? "Waiting for a player" : $"{state.Turn} to move";
        ConnectionStatus.Text = session!.Ready ? state.Status == RoomStatus.Waiting ? "Share your invitation to begin." : !opponentConnected ? "Waiting for your opponent to reconnect." : ownTurn ? "Your move." : "Your opponent's move."
            : session.Busy ? "Connecting…" : "Offline. Reconnect to resume this seat.";
        RoomLabel.Text = $"Room {state.Code}";
        StringBuilder history = new();
        for (int i = 0; i < state.Moves.Length; i += 2) history.AppendLine($"{i / 2 + 1,3}. {state.Moves[i],-12}{(i + 1 < state.Moves.Length ? state.Moves[i + 1] : "")}");
        History.Text = history.ToString();
        Reconnect.Visibility = !session.Ready && session.Pending == null ? Visibility.Visible : Visibility.Collapsed;
        Recover.Visibility = session.Pending != null ? Visibility.Visible : Visibility.Collapsed;
        Reconnect.IsEnabled = Recover.IsEnabled = !session.Busy;
        Resign.IsEnabled = Live;
        OfferDraw.IsEnabled = Live && ownTurn && opponentConnected && state.Draw.CanOffer;
        DrawResponse.Visibility = state.Draw.OfferedBy != null && state.Draw.OfferedBy != ticket.Seat && state.Status == RoomStatus.Active ? Visibility.Visible : Visibility.Collapsed;
        AcceptDraw.IsEnabled = DeclineDraw.IsEnabled = Live;
        OfferStatus.Text = state.Draw.OfferedBy == ticket.Seat ? "Draw offered · considered on the opponent's turn." : state.Draw.OfferedBy != null ? "Your opponent offered a draw." : "";
        ClaimDraw.Visibility = ownTurn && state.Draw.CanClaim ? Visibility.Visible : Visibility.Collapsed; ClaimDraw.IsEnabled = Live;
        ClaimIntended.Visibility = ownTurn && state.Draw.ClaimMoves.Length > 0 ? Visibility.Visible : Visibility.Collapsed; ClaimIntended.IsEnabled = Live;
        if (ClaimIntended.Visibility != Visibility.Visible) ClaimIntended.IsChecked = false;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (session?.Ticket != null && MessageBox.Show(this, "Closing this window loses its private seat credential. Leave the room?", "Leave online game", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) e.Cancel = true;
    }

    private void NewGame_Click(object sender, RoutedEventArgs e)
    {
        if (session?.Ticket != null && MessageBox.Show(this, "Leave this online seat and return to setup?", "New game", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Closing -= OnClosing;
        StartWindow start = new(); Application.Current.MainWindow = start; start.Show(); Close();
    }
}
