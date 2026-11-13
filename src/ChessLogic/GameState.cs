using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class GameState
    {
        private readonly List<MoveRecord> history = new();
        private readonly Dictionary<string, int> positionCounts = new();

        public Board Board { get; }
        public Player CurrentPlayer { get; private set; }
        public Result? Result { get; private set; }
        public ReadOnlyCollection<MoveRecord> History { get; }
        public int HalfMoveClock { get; private set; }
        public int CurrentPositionRepetitions { get; private set; }

        public GameState(Board board, Player currentPlayer)
        {
            ArgumentNullException.ThrowIfNull(board);
            if (currentPlayer != Player.White && currentPlayer != Player.Black)
            {
                throw new ArgumentOutOfRangeException(nameof(currentPlayer));
            }

            Board = board;
            CurrentPlayer = currentPlayer;
            History = history.AsReadOnly();
            RecordPosition();
            CheckForGameEnd();
        }

        public IEnumerable<Move> LegalMoves(Position pos)
        {
            if (IsGameOver() || pos == null || !Board.IsInside(pos) ||
                Board.IsEmpty(pos) || Board.GetPiece(pos).Colour != CurrentPlayer)
            {
                return Enumerable.Empty<Move>();
            }

            return MovesFrom(pos);
        }

        public void ApplyMove(Move move)
        {
            EnsureActive();
            Move legalMove = FindLegalMove(move);
            PieceType pieceType = Board.GetPiece(legalMove.From).Type;
            PieceType? capturedType = legalMove.Type == MoveType.EnPassant
                ? PieceType.Pawn : Board[legalMove.To]?.Type;

            HalfMoveClock = pieceType == PieceType.Pawn || capturedType.HasValue
                ? 0 : HalfMoveClock + 1;

            // The moving player's previous double step has now expired.
            Board.SetPawnSkipPosition(CurrentPlayer, null);
            legalMove.Execute(Board);
            history.Add(new MoveRecord(CurrentPlayer, pieceType, legalMove, capturedType));
            CurrentPlayer = CurrentPlayer.Opponent();
            RecordPosition();
            CheckForGameEnd();
        }

        public IEnumerable<Move> AllLegalMoves(Player player)
        {
            if (IsGameOver() || (player != Player.White && player != Player.Black))
            {
                return Enumerable.Empty<Move>();
            }

            return Board.PositionsOfPlayer(player).SelectMany(MovesFrom);
        }

        private IEnumerable<Move> MovesFrom(Position pos)
        {
            return Board.GetPiece(pos).GetMoves(pos, Board).Where(move => move.IsLegal(Board));
        }

        private Move FindLegalMove(Move move)
        {
            ArgumentNullException.ThrowIfNull(move);
            Move? legalMove = LegalMoves(move.From).FirstOrDefault(candidate =>
                candidate.To == move.To && candidate.Type == move.Type &&
                (candidate is not PawnPromotion promotion ||
                    move is PawnPromotion requested && promotion.newType == requested.newType));

            // Execute the generated move, never caller-supplied behavior.
            return legalMove ?? throw new ArgumentException("The move is not legal in this position.", nameof(move));
        }

        public bool CanClaimDraw(Move? intendedMove = null)
        {
            return !IsGameOver() && ClaimReason(intendedMove).HasValue;
        }

        public void ClaimDraw(Move? intendedMove = null)
        {
            EnsureActive();
            EndReason reason = ClaimReason(intendedMove)
                ?? throw new InvalidOperationException("No draw can be claimed in this position.");
            Result = Result.DrawByStalemate(reason);
        }

        private EndReason? ClaimReason(Move? intendedMove)
        {
            if (intendedMove == null)
            {
                if (CurrentPositionRepetitions >= 3) return EndReason.ThreefoldRepetition;
                if (HalfMoveClock >= 100) return EndReason.FiftyMoveRule;
                return null;
            }

            Move move = FindLegalMove(intendedMove);
            bool resetsClock = Board[move.From] is Pawn || !Board.IsEmpty(move.To) || move.Type == MoveType.EnPassant;
            Board copy = Board.Copy();
            copy.SetPawnSkipPosition(CurrentPlayer, null);
            move.Execute(copy);
            string key = PositionKey.Create(copy, CurrentPlayer.Opponent());

            if (positionCounts.GetValueOrDefault(key) >= 2) return EndReason.ThreefoldRepetition;
            if (!resetsClock && HalfMoveClock >= 99) return EndReason.FiftyMoveRule;
            return null;
        }

        public void Resign()
        {
            EnsureActive();
            Player opponent = CurrentPlayer.Opponent();
            bool opponentHasMaterial = Board.PositionsOfPlayer(opponent)
                .Any(pos => Board.GetPiece(pos).Type != PieceType.King);
            Result = opponentHasMaterial
                ? Result.WinByResignation(opponent)
                : Result.DrawByStalemate(EndReason.Resignation);
        }

        private void EnsureActive()
        {
            if (IsGameOver()) throw new InvalidOperationException("The game has already ended.");
        }

        private void RecordPosition()
        {
            string key = PositionKey.Create(Board, CurrentPlayer);
            CurrentPositionRepetitions = positionCounts.GetValueOrDefault(key) + 1;
            positionCounts[key] = CurrentPositionRepetitions;
        }

        private void CheckForGameEnd()
        {
            if (!AllLegalMoves(CurrentPlayer).Any())
            {
                if (Board.IsInCheck(CurrentPlayer))
                {
                    Result = Result.WinByMate(CurrentPlayer.Opponent());
                }
                else
                {
                    Result = Result.DrawByStalemate(EndReason.Stalemate);
                }
            }
            else if (Board.InsufficientMaterial())
            {
                Result = Result.DrawByStalemate(EndReason.InsufficientMaterial);
            }
            else if (CurrentPositionRepetitions >= 5)
            {
                Result = Result.DrawByStalemate(EndReason.FivefoldRepetition);
            }
            else if (HalfMoveClock >= 150)
            {
                Result = Result.DrawByStalemate(EndReason.SeventyFiveMoveRule);
            }
        }

        public bool IsGameOver()
        {
            return Result != null;
        }
    }
}
