using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessLogic
{
    public class GameState
    {
        private readonly List<MoveRecord> history = new();
        private readonly Dictionary<string, int> positionCounts = new();
        private readonly Stack<Snapshot> undoStates = new();
        private readonly Stack<Snapshot> redoStates = new();
        private readonly List<GameAction> actions = new();
        private readonly Dictionary<string, string> pgnTags = new(StringComparer.Ordinal);

        public Board Board { get; }
        public Player CurrentPlayer { get; private set; }
        public Result? Result { get; private set; }
        public ReadOnlyCollection<MoveRecord> History { get; }
        public int HalfMoveClock { get; private set; }
        public BigInteger FullMoveNumber { get; private set; }
        public int CurrentPositionRepetitions { get; private set; }
        public bool CanUndo => undoStates.Count > 0;
        public bool CanRedo => redoStates.Count > 0;
        public Player? DrawOfferedBy { get; private set; }
        public bool CanOfferDraw => !IsGameOver() && DrawOfferedBy == null && FullMoveNumber >= 2;
        public string InitialFen { get; }
        public IReadOnlyDictionary<string, string> PgnTags { get; }
        internal IReadOnlyList<GameAction> Actions => actions;
        internal int ActionCursor { get; private set; }

        public GameState(Board board, Player currentPlayer)
            : this(board, currentPlayer, 0, BigInteger.One)
        {
        }

        internal GameState(Board board, Player currentPlayer, int halfMoveClock, BigInteger fullMoveNumber)
        {
            ArgumentNullException.ThrowIfNull(board);
            if (currentPlayer != Player.White && currentPlayer != Player.Black)
            {
                throw new ArgumentOutOfRangeException(nameof(currentPlayer));
            }

            Board = board;
            CurrentPlayer = currentPlayer;
            HalfMoveClock = halfMoveClock;
            FullMoveNumber = fullMoveNumber;
            History = history.AsReadOnly();
            PgnTags = new ReadOnlyDictionary<string, string>(pgnTags);
            RecordPosition();
            CheckForGameEnd();
            InitialFen = ToFen();
        }

        public static GameState FromFen(string fen) => Fen.Parse(fen);

        public string ToFen() => Fen.Format(this);

        internal GameState CopyForAnalysis()
        {
            GameState copy = new(Board.Copy(), CurrentPlayer, HalfMoveClock, FullMoveNumber);
            copy.RestoreState(CaptureState());
            return copy;
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
            string san = San.Format(this, legalMove);
            PieceType pieceType = Board.GetPiece(legalMove.From).Type;
            PieceType? capturedType = legalMove.Type == MoveType.EnPassant
                ? PieceType.Pawn : Board[legalMove.To]?.Type;
            Snapshot previous = CaptureState();

            if (DrawOfferedBy == CurrentPlayer.Opponent()) DrawOfferedBy = null;

            HalfMoveClock = pieceType == PieceType.Pawn || capturedType.HasValue
                ? 0 : HalfMoveClock + 1;

            // The moving player's previous double step has now expired.
            Board.SetPawnSkipPosition(CurrentPlayer, null);
            legalMove.Execute(Board);
            history.Add(new MoveRecord(CurrentPlayer, pieceType, legalMove, capturedType, FullMoveNumber, san));
            if (CurrentPlayer == Player.Black) FullMoveNumber++;
            CurrentPlayer = CurrentPlayer.Opponent();
            RecordPosition();
            CheckForGameEnd();
            if (IsGameOver()) DrawOfferedBy = null;
            RecordUndo(previous, GameAction.ForMove("move", legalMove));
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

        public bool CanClaimDrawWithIntendedMove()
        {
            if (IsGameOver() || HalfMoveClock < 99 && !positionCounts.Values.Any(count => count >= 2)) return false;
            return AllLegalMoves(CurrentPlayer).Any(move => CanClaimDraw(move));
        }

        public void OfferDraw()
        {
            EnsureActive();
            if (!CanOfferDraw) throw new InvalidOperationException("Both players must have moved, and no draw offer may already be pending.");
            Snapshot previous = CaptureState();
            DrawOfferedBy = CurrentPlayer;
            RecordUndo(previous, new GameAction("offer"));
        }

        public bool CanRespondToDrawOffer(Player player)
        {
            return !IsGameOver() && DrawOfferedBy is Player offeredBy && player == offeredBy.Opponent() && FullMoveNumber >= 2;
        }

        public void AcceptDraw(Player player)
        {
            EnsureDrawResponder(player);
            Snapshot previous = CaptureState();
            Result = Result.DrawByStalemate(EndReason.DrawAgreement);
            DrawOfferedBy = null;
            RecordUndo(previous, new GameAction("accept", Responder: player));
        }

        public void DeclineDraw(Player player)
        {
            EnsureDrawResponder(player);
            Snapshot previous = CaptureState();
            DrawOfferedBy = null;
            RecordUndo(previous, new GameAction("decline", Responder: player));
        }

        private void EnsureDrawResponder(Player player)
        {
            EnsureActive();
            if (!CanRespondToDrawOffer(player)) throw new InvalidOperationException("Only the opponent can respond to a pending draw offer.");
        }

        public void ClaimDraw(Move? intendedMove = null)
        {
            EnsureActive();
            EndReason reason = ClaimReason(intendedMove)
                ?? throw new InvalidOperationException("No draw can be claimed in this position.");
            Snapshot previous = CaptureState();
            Result = Result.DrawByStalemate(reason);
            DrawOfferedBy = null;
            RecordUndo(previous, intendedMove == null ? new GameAction("claim") : GameAction.ForMove("claim", intendedMove));
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

        public void Resign() => Resign(CurrentPlayer);

        public void Resign(Player player)
        {
            EnsureActive();
            if (player is not (Player.White or Player.Black)) throw new ArgumentOutOfRangeException(nameof(player));
            Snapshot previous = CaptureState();
            Player opponent = player.Opponent();
            bool opponentHasMaterial = Board.PositionsOfPlayer(opponent)
                .Any(pos => Board.GetPiece(pos).Type != PieceType.King);
            Result = new Result(opponentHasMaterial ? opponent : Player.None, EndReason.Resignation, player);
            DrawOfferedBy = null;
            RecordUndo(previous, new GameAction("resign", Actor: player == CurrentPlayer ? null : player));
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            redoStates.Push(CaptureState());
            RestoreState(undoStates.Pop());
            ActionCursor--;
            return true;
        }

        internal void SetPgnTags(IEnumerable<KeyValuePair<string, string>> tags)
        {
            pgnTags.Clear();
            foreach (var tag in tags)
            {
                Pgn.ValidateTag(tag.Key, tag.Value);
                pgnTags.Add(tag.Key, tag.Value);
            }
        }

        internal void SetRecordedResult(Player winner)
        {
            EnsureActive();
            if (winner is not (Player.White or Player.Black or Player.None)) throw new ArgumentOutOfRangeException(nameof(winner));
            Snapshot previous = CaptureState();
            Result = new Result(winner, EndReason.RecordedResult);
            DrawOfferedBy = null;
            RecordUndo(previous, new GameAction("result", Winner: winner));
        }

        public bool Redo()
        {
            if (!CanRedo) return false;
            undoStates.Push(CaptureState());
            RestoreState(redoStates.Pop());
            ActionCursor++;
            return true;
        }

        private Snapshot CaptureState()
        {
            return new Snapshot(Board.Copy(), CurrentPlayer, Result, HalfMoveClock, FullMoveNumber,
                CurrentPositionRepetitions, history.ToArray(), new Dictionary<string, int>(positionCounts), DrawOfferedBy);
        }

        private void RecordUndo(Snapshot previous, GameAction action)
        {
            undoStates.Push(previous);
            redoStates.Clear();
            actions.RemoveRange(ActionCursor, actions.Count - ActionCursor);
            actions.Add(action);
            ActionCursor++;
        }

        private void RestoreState(Snapshot state)
        {
            // Restore copies so future moves cannot mutate a saved state.
            Board.RestoreFrom(state.Board);
            CurrentPlayer = state.Player;
            Result = state.Result;
            HalfMoveClock = state.HalfMoveClock;
            FullMoveNumber = state.FullMoveNumber;
            CurrentPositionRepetitions = state.Repetitions;
            DrawOfferedBy = state.OfferedBy;
            history.Clear();
            history.AddRange(state.History);
            positionCounts.Clear();
            foreach (var entry in state.PositionCounts) positionCounts.Add(entry.Key, entry.Value);
        }

        private sealed record Snapshot(Board Board, Player Player, Result? Result, int HalfMoveClock,
            BigInteger FullMoveNumber, int Repetitions, MoveRecord[] History, Dictionary<string, int> PositionCounts, Player? OfferedBy);

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
