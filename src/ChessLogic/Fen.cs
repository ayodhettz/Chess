using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace ChessLogic
{
    public static class Fen
    {
        public const string InitialPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public static GameState Parse(string fen)
        {
            ArgumentNullException.ThrowIfNull(fen);
            if (fen.Length > 1024) throw new FormatException("FEN cannot exceed 1024 characters.");
            string[] fields = fen.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6) throw new FormatException("FEN must contain exactly six fields.");
            if (fields.Any(field => field.Any(symbol => symbol is < '!' or > '~')))
            {
                throw new FormatException("FEN fields must use printable ASCII characters.");
            }

            Board board = ParsePlacement(fields[0]);
            Player player = fields[1] switch
            {
                "w" => Player.White,
                "b" => Player.Black,
                _ => throw new FormatException("The active colour must be w or b.")
            };
            ParseCastling(board, fields[2]);

            if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out int halfMoves))
            {
                throw new FormatException("The half-move clock must be an integer from 0 to 2147483647.");
            }
            if (!BigInteger.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger fullMoves) || fullMoves < 1)
            {
                throw new FormatException("The full-move number must be a positive integer.");
            }

            ParseEnPassant(board, player, fields[3], halfMoves);
            if (board.IsInCheck(player.Opponent()))
            {
                throw new FormatException("The side that just moved cannot be in check.");
            }

            return new GameState(board, player, halfMoves, fullMoves);
        }

        public static bool TryParse(string? fen, [NotNullWhen(true)] out GameState? game, out string? error)
        {
            game = null;
            if (string.IsNullOrWhiteSpace(fen))
            {
                error = "Enter a FEN position.";
                return false;
            }
            try
            {
                game = Parse(fen);
                error = null;
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static string Format(GameState game)
        {
            ArgumentNullException.ThrowIfNull(game);
            StringBuilder text = new();
            for (int row = 0; row < 8; row++)
            {
                if (row > 0) text.Append('/');
                int empty = 0;
                for (int col = 0; col < 8; col++)
                {
                    if (game.Board[row, col] is not Piece piece)
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0) text.Append((char)('0' + empty));
                    empty = 0;
                    text.Append(PieceSymbol(piece));
                }
                if (empty > 0) text.Append((char)('0' + empty));
            }

            text.Append(game.CurrentPlayer == Player.White ? " w " : " b ");
            string rights = CastlingRights(game.Board);
            text.Append(rights.Length == 0 ? "-" : rights);

            // FEN records every double step, even when no capture is available.
            Position? skip = game.Board.GetPawnSkipPosition(game.CurrentPlayer.Opponent());
            text.Append(' ').Append(skip == null ? "-" : $"{(char)('a' + skip.Column)}{(char)('8' - skip.Row)}");
            text.Append(' ').Append(game.HalfMoveClock.ToString(CultureInfo.InvariantCulture));
            text.Append(' ').Append(game.FullMoveNumber.ToString(CultureInfo.InvariantCulture));
            return text.ToString();
        }

        private static Board ParsePlacement(string placement)
        {
            string[] ranks = placement.Split('/');
            if (ranks.Length != 8) throw new FormatException("Piece placement must contain eight ranks.");
            Board board = new();
            for (int row = 0; row < 8; row++)
            {
                int col = 0;
                bool previousWasDigit = false;
                foreach (char symbol in ranks[row])
                {
                    if (symbol is >= '1' and <= '8')
                    {
                        if (previousWasDigit) throw new FormatException("Use one digit for each run of empty squares.");
                        col += symbol - '0';
                        previousWasDigit = true;
                    }
                    else
                    {
                        if (col >= 8) throw new FormatException("Each rank must describe exactly eight squares.");
                        Piece piece = CreatePiece(symbol);
                        if (piece is Pawn && (row == 0 || row == 7))
                        {
                            throw new FormatException("Pawns cannot occupy the first or eighth rank.");
                        }
                        piece.HasMoved = piece is not Pawn || row != (piece.Colour == Player.White ? 6 : 1);
                        board[row, col++] = piece;
                        previousWasDigit = false;
                    }
                    if (col > 8) throw new FormatException("Each rank must describe exactly eight squares.");
                }
                if (col != 8) throw new FormatException("Each rank must describe exactly eight squares.");
            }

            Counting counts = board.CountPieces();
            if (counts.White(PieceType.King) != 1 || counts.Black(PieceType.King) != 1)
            {
                throw new FormatException("A position must contain exactly one king of each colour.");
            }
            foreach (Player player in new[] { Player.White, Player.Black })
            {
                int pawns = player == Player.White ? counts.White(PieceType.Pawn) : counts.Black(PieceType.Pawn);
                if (board.PositionsOfPlayer(player).Count() > 16 || pawns > 8)
                {
                    throw new FormatException("Each side can have at most sixteen pieces and eight pawns.");
                }
            }
            return board;
        }

        private static Piece CreatePiece(char symbol)
        {
            if (!"PNBRQKpnbrqk".Contains(symbol)) throw new FormatException("Piece placement contains an unknown piece.");
            Player player = char.IsUpper(symbol) ? Player.White : Player.Black;
            return char.ToLowerInvariant(symbol) switch
            {
                'p' => new Pawn(player),
                'n' => new Knight(player),
                'b' => new Bishop(player),
                'r' => new Rook(player),
                'q' => new Queen(player),
                'k' => new King(player),
                _ => throw new FormatException("Piece placement contains an unknown piece.")
            };
        }

        private static char PieceSymbol(Piece piece)
        {
            char symbol = piece.Type switch
            {
                PieceType.Pawn => 'p', PieceType.Knight => 'n', PieceType.Bishop => 'b',
                PieceType.Rook => 'r', PieceType.Queen => 'q', PieceType.King => 'k',
                _ => throw new InvalidOperationException("Unknown piece type.")
            };
            return piece.Colour == Player.White ? char.ToUpperInvariant(symbol) : symbol;
        }

        private static void ParseCastling(Board board, string rights)
        {
            if (rights == "-") return;
            HashSet<char> seen = new();
            foreach (char right in rights)
            {
                if (!"KQkq".Contains(right) || !seen.Add(right))
                {
                    throw new FormatException("Castling rights must be unique letters from KQkq, or -.");
                }
                Player player = char.IsUpper(right) ? Player.White : Player.Black;
                int row = player == Player.White ? 7 : 0;
                int rookCol = char.ToLowerInvariant(right) == 'k' ? 7 : 0;
                if (board[row, 4] is not King king || king.Colour != player ||
                    board[row, rookCol] is not Rook rook || rook.Colour != player)
                {
                    throw new FormatException("Castling rights require the matching king and rook on their starting squares.");
                }
                king.HasMoved = false;
                rook.HasMoved = false;
            }
        }

        private static string CastlingRights(Board board)
        {
            StringBuilder rights = new();
            foreach (Player player in new[] { Player.White, Player.Black })
            {
                int row = player == Player.White ? 7 : 0;
                if (board[row, 4] is not King king || king.Colour != player || king.HasMoved) continue;
                foreach (int col in new[] { 7, 0 })
                {
                    if (board[row, col] is Rook rook && rook.Colour == player && !rook.HasMoved)
                    {
                        char right = col == 7 ? 'k' : 'q';
                        rights.Append(player == Player.White ? char.ToUpperInvariant(right) : right);
                    }
                }
            }
            return rights.ToString();
        }

        private static void ParseEnPassant(Board board, Player player, string field, int halfMoves)
        {
            if (field == "-") return;
            char rank = player == Player.White ? '6' : '3';
            if (field.Length != 2 || field[0] is < 'a' or > 'h' || field[1] != rank)
            {
                throw new FormatException("The en passant target must be a file a–h on rank 6 for White to move, or rank 3 for Black.");
            }
            Position target = new(8 - (rank - '0'), field[0] - 'a');
            int step = player == Player.White ? 1 : -1;
            Position pawnPos = new(target.Row + step, target.Column);
            Position origin = new(target.Row - step, target.Column);
            if (!board.IsEmpty(target) || !board.IsEmpty(origin) || halfMoves != 0 ||
                board[pawnPos] is not Pawn pawn || pawn.Colour != player.Opponent())
            {
                throw new FormatException("The en passant target must follow an opposing pawn double step with a zero half-move clock.");
            }
            board.SetPawnSkipPosition(player.Opponent(), target);
        }
    }
}
