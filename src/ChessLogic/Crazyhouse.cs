namespace ChessLogic;

public enum GameVariant { Standard, Crazyhouse }

public partial class Board
{
    private readonly int[,] pockets = new int[3, 6];
    public GameVariant Variant { get; internal set; }
    public int PocketCount(Player player, PieceType type) => pockets[(int)player, (int)type];
    internal void AddToPocket(Player player, PieceType type, int count = 1) => pockets[(int)player, (int)type] += count;

    internal void ExecuteMove(Move move, Player player)
    {
        if (move is DropMove drop) AddToPocket(player, drop.PieceType, -1);
        else if (Variant == GameVariant.Crazyhouse)
        {
            Position captured = move.Type == MoveType.EnPassant ? new(move.From.Row, move.To.Column) : move.To;
            if (this[captured] is Piece victim)
                AddToPocket(player, victim.IsPromoted ? PieceType.Pawn : victim.Type);
        }
        move.Execute(this);
    }

    internal void CopyVariantFrom(Board source)
    {
        Variant = source.Variant;
        Array.Copy(source.pockets, pockets, pockets.Length);
    }

    internal static Piece CreatePiece(PieceType type, Player player) => type switch
    {
        PieceType.Pawn => new Pawn(player), PieceType.Knight => new Knight(player),
        PieceType.Bishop => new Bishop(player), PieceType.Rook => new Rook(player),
        PieceType.Queen => new Queen(player), _ => throw new ArgumentException("Only pocket pieces can be dropped.")
    };
}

public sealed class DropMove(PieceType pieceType, Player player, Position to) : Move
{
    public PieceType PieceType { get; } = pieceType;
    public Player Player { get; } = player;
    public override MoveType Type => MoveType.Drop;
    // A drop has no board origin. Consumers must handle DropMove before reading From.
    public override Position From => new(-1, (int)PieceType);
    public override Position To { get; } = to;

    public override void Execute(Board board)
    {
        Piece piece = Board.CreatePiece(PieceType, Player);
        piece.HasMoved = PieceType != PieceType.Pawn || To.Row != (Player == Player.White ? 6 : 1);
        board[To] = piece;
    }

    public override bool IsLegal(Board board)
    {
        if (board.Variant != GameVariant.Crazyhouse || Player is not (Player.White or Player.Black) ||
            !Enum.IsDefined(PieceType) || PieceType == PieceType.King || !Board.IsInside(To) ||
            !board.IsEmpty(To) || board.PocketCount(Player, PieceType) <= 0 ||
            PieceType == PieceType.Pawn && To.Row is 0 or 7) return false;
        Board copy = board.Copy();
        Execute(copy);
        return !copy.IsInCheck(Player);
    }
}

public partial class GameState
{
    public GameVariant Variant => Board.Variant;

    public IEnumerable<Move> LegalDrops(PieceType type) => IsGameOver()
        ? Enumerable.Empty<Move>() : DropsFor(CurrentPlayer, type);

    private IEnumerable<Move> DropsFor(Player player, PieceType? selected = null)
    {
        if (Variant != GameVariant.Crazyhouse) yield break;
        foreach (PieceType type in Enum.GetValues<PieceType>().Where(type => type != PieceType.King &&
            (!selected.HasValue || selected == type) && Board.PocketCount(player, type) > 0))
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 8; col++)
                {
                    DropMove drop = new(type, player, new(row, col));
                    if (drop.IsLegal(Board)) yield return drop;
                }
    }
}
