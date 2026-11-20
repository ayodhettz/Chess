using System.Security.Cryptography;
using System.Text;
using Chess.Contracts;
using ChessLogic;
using Microsoft.Extensions.Options;

namespace Chess.Server;

public sealed record RoomUpdate(RoomSnapshot State, string[] Recipients);
public sealed record RoomLease(SeatTicket Ticket, RoomUpdate Update);

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

    public RoomLease Create(string connectionId, Seat preferredSeat)
    {
        lock (gate)
        {
            Connection connection = Unseated(connectionId);
            if (!Enum.IsDefined(preferredSeat)) throw Error("invalid_seat", "Choose White or Black.");
            if (rooms.Count >= limits.MaxRooms) throw Error("capacity", "The server is at room capacity.");
            string code;
            do { code = string.Concat(Enumerable.Range(0, 8).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[RandomNumberGenerator.GetInt32(32)])); }
            while (rooms.ContainsKey(code));
            Room room = new(code, clock.GetUtcNow());
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
        lock (gate)
        {
            var (room, seat) = Seated(connectionId);
            if (request == null || request.Move == null) throw Error("invalid_move", "A move is required.");
            if (request.ExpectedRevision != room.Revision) throw Error("stale_revision", "Refresh the room before submitting another move.");
            if (Status(room) is RoomStatus.Finished or RoomStatus.Closed) throw Error("game_finished", "This game has ended.");
            if (room.White?.ConnectionId == null || room.Black?.ConnectionId == null) throw Error("opponent_unavailable", "Both players must be connected.");
            if (seat != ToSeat(room.Game.CurrentPlayer)) throw Error("wrong_turn", "It is not your turn.");
            Position from = Square(request.Move.From), to = Square(request.Move.To);
            PieceType? promotion = request.Move.Promotion switch
            {
                null => null, Promotion.Queen => PieceType.Queen, Promotion.Rook => PieceType.Rook,
                Promotion.Bishop => PieceType.Bishop, Promotion.Knight => PieceType.Knight,
                _ => throw Error("invalid_move", "The promotion choice is invalid.")
            };
            Move? move = room.Game.LegalMoves(from).SingleOrDefault(candidate => candidate.To == to &&
                (candidate is PawnPromotion pawn ? pawn.newType == promotion : promotion == null));
            if (move == null) throw Error("invalid_move", "The move is not legal in this position.");
            // Execute the server-generated move, never client-supplied behavior.
            room.Game.ApplyMove(move);
            room.Revision++;
            if (!room.Game.IsGameOver() && room.Game.History.Count >= limits.MaxPlies) room.ClosedReason = "move_limit";
            return Update(room);
        }
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
        return new(OnlineProtocol.Version, room.Code, room.Revision, status,
            room.White?.ConnectionId != null, room.Black?.ConnectionId != null, ToSeat(game.CurrentPlayer), game.ToFen(),
            game.History.Select(move => move.San).ToArray(),
            status == RoomStatus.Active ? game.AllLegalMoves(game.CurrentPlayer).Select(move => new MoveIntent(Name(move.From), Name(move.To),
                move is PawnPromotion pawn ? Enum.Parse<Promotion>(pawn.newType.ToString()) : null)).ToArray() : [],
            game.Result == null ? null : new(game.Result.Winner == Player.None ? null : ToSeat(game.Result.Winner), game.Result.Reason.ToString()),
            room.ClosedReason, Expiry(room));
    }

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
        public string? ClosedReason { get; set; }
    }
    private sealed class PlayerSeat(byte[] digest)
    {
        public byte[] Digest { get; } = digest;
        public string? ConnectionId { get; set; }
    }
    private sealed class Connection(DateTimeOffset window)
    {
        public Room? Room { get; set; }
        public Seat Seat { get; set; }
        public DateTimeOffset Window { get; set; } = window;
        public int Calls { get; set; }
    }
}
