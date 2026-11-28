using System.Security.Cryptography;
using System.Text;
using Chess.Contracts;
using ChessLogic;
using Microsoft.Extensions.Options;

namespace Chess.Server;

public sealed record RoomUpdate(RoomSnapshot State, string[] Recipients);
public sealed record RoomLease(SeatTicket Ticket, RoomUpdate Update);
public sealed record ComputerWork(string Code, long Revision, GameState Position, BotDifficulty Difficulty = BotDifficulty.Medium);
public sealed record CommandExecution(CommandResult Result, RoomUpdate? Update);

public sealed class RoomDirectory(TimeProvider clock, IOptions<RoomLimits> configuredLimits)
{
    // Seat bindings and game state must change atomically.
    private readonly object gate = new();
    private readonly Dictionary<string, Room> rooms = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Connection> connections = new(StringComparer.Ordinal);
    private readonly RoomLimits limits = configuredLimits.Value;

    public void Connect(string connectionId)
    {
        lock (gate)
        {
            if (connections.Count >= limits.MaxConnections) throw Error("capacity", "The server is at connection capacity.");
            connections.Add(connectionId, new Connection(clock.GetUtcNow()));
        }
    }

    public RoomLease Create(string connectionId, Seat preferredSeat, bool computer = false, BotDifficulty difficulty = BotDifficulty.Medium)
    {
        lock (gate)
        {
            _ = BotSearchLimits.For(difficulty);
            Connection connection = Unseated(connectionId);
            if (!Enum.IsDefined(preferredSeat)) throw Error("invalid_seat", "Choose White or Black.");
            if (rooms.Count >= limits.MaxRooms) throw Error("capacity", "The server is at room capacity.");
            string code;
            do { code = string.Concat(Enumerable.Range(0, 8).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[RandomNumberGenerator.GetInt32(32)])); }
            while (rooms.ContainsKey(code));
            Room room = new(code, clock.GetUtcNow()) { Difficulty = difficulty };
            if (computer)
            {
                room.ComputerSeat = preferredSeat == Seat.White ? Seat.Black : Seat.White;
                PlayerSeat opponent = new(RandomNumberGenerator.GetBytes(32)) { IsComputer = true };
                if (room.ComputerSeat == Seat.White) room.White = opponent; else room.Black = opponent;
            }
            rooms.Add(code, room);
            return Bind(room, preferredSeat, connectionId, connection, resume: false);
        }
    }

    public RoomLease Join(string connectionId, string code)
    {
        lock (gate)
        {
            Connection connection = Unseated(connectionId);
            Room room = Find(code);
            Seat? available = room.White == null ? Seat.White : room.Black == null ? Seat.Black : null;
            if (available == null) throw Error("room_full", "Both player seats are reserved.");
            return Bind(room, available.Value, connectionId, connection, resume: false);
        }
    }

    public RoomLease Resume(string connectionId, string code, string credential)
    {
        lock (gate)
        {
            Connection connection = Unseated(connectionId);
            Room room = Find(code);
            byte[] digest = Hash(credential);
            Seat? seat = room.White != null && CryptographicOperations.FixedTimeEquals(room.White.Digest, digest) ? Seat.White :
                room.Black != null && CryptographicOperations.FixedTimeEquals(room.Black.Digest, digest) ? Seat.Black : null;
            if (seat == null) throw Error("invalid_credential", "The seat credential is invalid.");
            RoomLease lease = Bind(room, seat.Value, connectionId, connection, resume: true);
            return lease with { Ticket = lease.Ticket with { Credential = credential } };
        }
    }

    public RoomSnapshot Snapshot(string connectionId)
    {
        lock (gate) { return State(Seated(connectionId).Room); }
    }

    public RoomUpdate Move(string connectionId, MoveRequest request)
    {
        if (request == null || request.Move == null) throw Error("invalid_move", "A move is required.");
        return Submit(connectionId, new(Guid.NewGuid(), request.ExpectedRevision, OnlineAction.Move, request.Move)).Update!;
    }

    public CommandExecution Submit(string connectionId, RoomCommand command)
    {
        lock (gate)
        {
            var (room, seat) = Seated(connectionId);
            if (command == null || command.Id == Guid.Empty) throw Error("invalid_command", "A non-empty command ID is required.");
            PlayerSeat actor = (seat == Seat.White ? room.White : room.Black)!;
            // A retry is recognized before checking the now-outdated revision or result.
            if (actor.Receipts.TryGetValue(command.Id, out Receipt? receipt))
            {
                if (receipt.Command != command) throw Error("command_conflict", "This command ID was used with different arguments.");
                return new(new(command.Id, true, receipt.Revision, State(room)), null);
            }
            if (command.ExpectedRevision != room.Revision) throw Error("stale_revision", "Refresh the room before submitting another command.");
            if (Status(room) is RoomStatus.Finished or RoomStatus.Closed) throw Error("game_finished", "This game has ended.");
            if (Status(room) == RoomStatus.Waiting) throw Error("game_not_started", "Both seats must be occupied.");
            if (!Enum.IsDefined(command.Action) || command.Move != null && command.Action is not (OnlineAction.Move or OnlineAction.ClaimDraw))
                throw Error("invalid_command", "The command arguments are invalid.");
            if ((command.Action is OnlineAction.Move or OnlineAction.OfferDraw) &&
                (!Connected(room.White) || !Connected(room.Black)))
                throw Error("opponent_unavailable", "Both players must be connected.");
            if ((command.Action is OnlineAction.Move or OnlineAction.OfferDraw or OnlineAction.ClaimDraw) && seat != ToSeat(room.Game.CurrentPlayer))
                throw Error("wrong_turn", "It is not your turn.");
            Player player = seat == Seat.White ? Player.White : Player.Black;
            try
            {
                switch (command.Action)
                {
                    case OnlineAction.Move: room.Game.ApplyMove(Resolve(room.Game, command.Move)); break;
                    case OnlineAction.OfferDraw: room.Game.OfferDraw(); break;
                    case OnlineAction.AcceptDraw: room.Game.AcceptDraw(player); break;
                    case OnlineAction.DeclineDraw: room.Game.DeclineDraw(player); break;
                    case OnlineAction.ClaimDraw: room.Game.ClaimDraw(command.Move == null ? null : Resolve(room.Game, command.Move)); break;
                    case OnlineAction.Resign: room.Game.Resign(player); break;
                }
            }
            catch (InvalidOperationException error) { throw Error("invalid_action", error.Message); }
            room.Revision++;
            room.Commands++;
            if (!room.Game.IsGameOver())
            {
                if (room.Game.History.Count >= limits.MaxPlies) room.ClosedReason = "move_limit";
                else if (room.Commands >= limits.MaxCommands) room.ClosedReason = "command_limit";
            }
            actor.Receipts.Add(command.Id, new(command, room.Revision));
            actor.ReceiptOrder.Enqueue(command.Id);
            if (actor.ReceiptOrder.Count > 128) actor.Receipts.Remove(actor.ReceiptOrder.Dequeue());
            RoomUpdate update = Update(room);
            return new(new(command.Id, false, room.Revision, update.State), update);
        }
    }

    private static bool Connected(PlayerSeat? player) => player?.IsComputer == true || player?.ConnectionId != null;

    public ComputerWork[] TakeComputerTurns()
    {
        lock (gate)
        {
            List<ComputerWork> work = [];
            foreach (Room room in rooms.Values)
            {
                if (room.ComputerBusy || room.ComputerSeat != ToSeat(room.Game.CurrentPlayer) || Expired(room) ||
                    Status(room) != RoomStatus.Active || !Connected(room.White) || !Connected(room.Black)) continue;
                room.ComputerBusy = true;
                work.Add(new(room.Code, room.Revision, room.Game.CopyForAnalysis(), room.Difficulty));
            }
            return work.ToArray();
        }
    }

    public RoomUpdate? CompleteComputerTurn(ComputerWork work, BotDecision? decision)
    {
        lock (gate)
        {
            if (!rooms.TryGetValue(work.Code, out Room? room)) return null;
            room.ComputerBusy = false;
            // Presence changes, resignation, and expiry invalidate an in-flight search.
            if (Expired(room) || room.Revision != work.Revision || Status(room) != RoomStatus.Active ||
                room.ComputerSeat != ToSeat(room.Game.CurrentPlayer)) return null;
            if (decision == null) room.ClosedReason = "computer_unavailable";
            else if (decision.AcceptDraw) room.Game.AcceptDraw(room.Game.CurrentPlayer);
            else if (decision.ClaimDraw) room.Game.ClaimDraw(decision.Move == null ? null : Resolve(room.Game, Intent(decision.Move)));
            else if (decision.Move != null) room.Game.ApplyMove(Resolve(room.Game, Intent(decision.Move)));
            else room.ClosedReason = "computer_unavailable";
            room.Revision++;
            room.Commands++;
            if (!room.Game.IsGameOver())
            {
                if (room.Game.History.Count >= limits.MaxPlies) room.ClosedReason = "move_limit";
                else if (room.Commands >= limits.MaxCommands) room.ClosedReason = "command_limit";
            }
            return Update(room);
        }
    }

    private static Move Resolve(GameState game, MoveIntent? intent)
    {
        if (intent == null) throw Error("invalid_move", "A move is required.");
        Position from = Square(intent.From), to = Square(intent.To);
        PieceType? promotion = intent.Promotion switch
        {
            null => null, Promotion.Queen => PieceType.Queen, Promotion.Rook => PieceType.Rook,
            Promotion.Bishop => PieceType.Bishop, Promotion.Knight => PieceType.Knight,
            _ => throw Error("invalid_move", "The promotion choice is invalid.")
        };
        return game.LegalMoves(from).SingleOrDefault(candidate => candidate.To == to &&
            (candidate is PawnPromotion pawn ? pawn.newType == promotion : promotion == null))
            ?? throw Error("invalid_move", "The move is not legal in this position.");
    }

    public RoomUpdate? Disconnect(string connectionId)
    {
        lock (gate)
        {
            if (!connections.Remove(connectionId, out Connection? connection) || connection.Room == null) return null;
            Room room = connection.Room;
            if (room.White?.ConnectionId == connectionId) room.White.ConnectionId = null;
            if (room.Black?.ConnectionId == connectionId) room.Black.ConnectionId = null;
            room.Revision++;
            return Update(room);
        }
    }

    public RoomUpdate[] ExpireRooms()
    {
        lock (gate)
        {
            Room[] expired = rooms.Values.Where(Expired).ToArray();
            RoomUpdate[] updates = new RoomUpdate[expired.Length];
            for (int index = 0; index < expired.Length; index++)
            {
                Room room = expired[index];
                room.ClosedReason = "expired";
                room.Revision++;
                updates[index] = Update(room);
                foreach (string id in updates[index].Recipients)
                    if (connections.TryGetValue(id, out Connection? connection)) connection.Room = null;
                rooms.Remove(room.Code);
            }
            return updates;
        }
    }

    private RoomLease Bind(Room room, Seat seat, string connectionId, Connection connection, bool resume)
    {
        PlayerSeat? player = seat == Seat.White ? room.White : room.Black;
        string credential = "";
        if (!resume)
        {
            credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            player = new PlayerSeat(Hash(credential));
            if (seat == Seat.White) room.White = player; else room.Black = player;
        }
        if (player!.ConnectionId is string oldId && connections.TryGetValue(oldId, out Connection? old)) old.Room = null;
        player.ConnectionId = connectionId;
        connection.Room = room;
        connection.Seat = seat;
        room.Revision++;
        RoomUpdate update = Update(room);
        return new(new SeatTicket(room.Code, seat, credential, update.State), update);
    }

    private Connection Unseated(string id)
    {
        Connection connection = CheckConnection(id);
        if (connection.Room != null) throw Error("already_seated", "Leave this connection before joining another room.");
        return connection;
    }

    private (Room Room, Seat Seat) Seated(string id)
    {
        Connection connection = CheckConnection(id);
        if (connection.Room == null) throw Error("not_seated", "Join a room first.");
        Room room = Find(connection.Room.Code);
        return (room, connection.Seat);
    }

    private Connection CheckConnection(string id)
    {
        if (!connections.TryGetValue(id, out Connection? connection)) throw Error("not_connected", "The connection is unavailable.");
        DateTimeOffset now = clock.GetUtcNow();
        if (now - connection.Window >= TimeSpan.FromSeconds(10)) { connection.Window = now; connection.Calls = 0; }
        if (++connection.Calls > 60) throw Error("rate_limit", "Too many room requests. Try again shortly.");
        return connection;
    }

    private Room Find(string? code)
    {
        if (code == null || code.Length > 16 || !rooms.TryGetValue(code.Trim().ToUpperInvariant(), out Room? room))
            throw Error("room_unavailable", "The room is unavailable.");
        if (Expired(room)) throw Error("room_expired", "This room has expired.");
        return room;
    }

    private bool Expired(Room room) => clock.GetUtcNow() >= Expiry(room);
    private DateTimeOffset Expiry(Room room) => room.Created + (room.White == null || room.Black == null ? limits.WaitingLifetime : limits.Lifetime);
    private static RoomStatus Status(Room room) => room.ClosedReason != null ? RoomStatus.Closed : room.Game.IsGameOver() ? RoomStatus.Finished :
        room.White == null || room.Black == null ? RoomStatus.Waiting : RoomStatus.Active;
    private static Seat ToSeat(Player player) => player == Player.White ? Seat.White : Seat.Black;
    private static string Name(Position position) => $"{(char)('a' + position.Column)}{8 - position.Row}";

    private RoomSnapshot State(Room room)
    {
        GameState game = room.Game;
        RoomStatus status = Status(room);
        bool active = status == RoomStatus.Active;
        Move[] legal = active ? game.AllLegalMoves(game.CurrentPlayer).ToArray() : [];
        bool connected = Connected(room.White) && Connected(room.Black);
        return new(OnlineProtocol.Version, room.Code, room.Revision, status,
            Connected(room.White), Connected(room.Black), ToSeat(game.CurrentPlayer), game.ToFen(),
            game.History.Select(move => move.San).ToArray(),
            legal.Select(Intent).ToArray(),
            game.Result == null ? null : new(game.Result.Winner == Player.None ? null : ToSeat(game.Result.Winner), game.Result.Reason.ToString(),
                game.Result.ResignedBy is Player resigned ? ToSeat(resigned) : null),
            room.ClosedReason, Expiry(room), new(game.DrawOfferedBy is Player offer ? ToSeat(offer) : null,
                active && connected && game.CanOfferDraw, active && game.CanClaimDraw(),
                active && game.CanClaimDrawWithIntendedMove() ? legal.Where(move => game.CanClaimDraw(move)).Select(Intent).ToArray() : []), room.ComputerSeat);
    }

    private static MoveIntent Intent(Move move) => new(Name(move.From), Name(move.To),
        move is PawnPromotion pawn ? Enum.Parse<Promotion>(pawn.newType.ToString()) : null);

    private RoomUpdate Update(Room room) => new(State(room), new[] { room.White?.ConnectionId, room.Black?.ConnectionId }.OfType<string>().ToArray());
    private static byte[] Hash(string? credential)
    {
        if (credential == null || credential.Length != 64) throw Error("invalid_credential", "The seat credential is invalid.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(credential));
    }
    private static Position Square(string? value)
    {
        if (value == null || value.Length != 2 || value[0] is < 'a' or > 'h' || value[1] is < '1' or > '8')
            throw Error("invalid_move", "Use squares from a1 to h8.");
        return new('8' - value[1], value[0] - 'a');
    }
    private static RoomException Error(string code, string message) => new(code, message);

    private sealed class Room(string code, DateTimeOffset created)
    {
        public string Code { get; } = code;
        public DateTimeOffset Created { get; } = created;
        public GameState Game { get; } = new(Board.Initial(), Player.White);
        public PlayerSeat? White { get; set; }
        public PlayerSeat? Black { get; set; }
        public long Revision { get; set; }
        public int Commands { get; set; }
        public string? ClosedReason { get; set; }
        public Seat? ComputerSeat { get; set; }
        public bool ComputerBusy { get; set; }
        public BotDifficulty Difficulty { get; init; }
    }
    private sealed class PlayerSeat(byte[] digest)
    {
        public byte[] Digest { get; } = digest;
        public bool IsComputer { get; init; }
        public string? ConnectionId { get; set; }
        public Dictionary<Guid, Receipt> Receipts { get; } = new();
        public Queue<Guid> ReceiptOrder { get; } = new();
    }
    private sealed record Receipt(RoomCommand Command, long Revision);
    private sealed class Connection(DateTimeOffset window)
    {
        public Room? Room { get; set; }
        public Seat Seat { get; set; }
        public DateTimeOffset Window { get; set; } = window;
        public int Calls { get; set; }
    }
}
