using ChessLogic;

namespace Chess.Tests;

public class CrazyhouseTests
{
    private static void Play(GameState game, string san) => game.ApplyMove(San.Parse(game, san));

    [Fact]
    public void CapturesChangeColourAndDropsConsumeOnlyTheOwnersPocket()
    {
        GameState game = Fen.Parse(Fen.CrazyhouseInitialPosition);
        foreach (string san in new[] { "e4", "d5", "exd5", "Qxd5" }) Play(game, san);
        Assert.Equal(1, game.Board.PocketCount(Player.White, PieceType.Pawn));
        Assert.Equal(1, game.Board.PocketCount(Player.Black, PieceType.Pawn));
        string before = game.ToFen();
        Play(game, "P@e4");
        Assert.IsType<Pawn>(game.Board[new Position(4, 4)]);
        Assert.Equal(Player.White, game.Board.GetPiece(new(4, 4)).Colour);
        Assert.Equal(0, game.Board.PocketCount(Player.White, PieceType.Pawn));
        game.Undo(); Assert.Equal(before, game.ToFen());
        game.Redo(); Assert.Equal(0, game.Board.PocketCount(Player.White, PieceType.Pawn));
        Assert.Equal(game.ToFen(), GameSave.Deserialize(GameSave.Serialize(game)).ToFen());
        Assert.Equal(game.ToFen(), Pgn.Parse(Pgn.Format(game)).ToFen());
    }

    [Fact]
    public void PromotedPiecesReturnAsPawnsAndSurviveHistoryAndFen()
    {
        GameState game = Fen.Parse("7k/8/8/8/8/4q~3/4R3/4K3[] w - - 0 1");
        Assert.Contains("q~", game.CopyForAnalysis().ToFen());
        Play(game, "Rxe3");
        Assert.Equal(1, game.Board.PocketCount(Player.White, PieceType.Pawn));
        Assert.Equal(0, game.Board.PocketCount(Player.White, PieceType.Queen));
        game.Undo(); Assert.Contains("q~", game.ToFen());
        game.Redo(); Assert.Equal(1, game.Board.PocketCount(Player.White, PieceType.Pawn));
    }

    [Fact]
    public void DropsCanPreventMateAndMayGiveImmediateMate()
    {
        GameState blocked = Fen.Parse("6rk/6p1/8/8/8/8/8/K6R[n] b - - 0 1");
        Assert.False(blocked.IsGameOver());
        Assert.Equal(6, blocked.LegalDrops(PieceType.Knight).Count());
        Play(blocked, "N@h7"); Assert.Null(blocked.CheckedKingSquare);
        GameState mate = Fen.Parse("7k/8/5KQ1/8/8/8/8/8[R] w - - 0 1");
        Play(mate, "R@h7#");
        Assert.Equal(EndReason.Checkmate, mate.Result!.Reason);
        Assert.Equal(Player.White, mate.Result.Winner);
    }

    [Fact]
    public void PawnDropsCannotUseBackRanksAndKnightCheckCannotBeBlocked()
    {
        GameState game = Fen.Parse("4k3/8/8/8/8/8/8/4K3[Pp] w - - 0 1");
        Assert.Equal(48, game.LegalDrops(PieceType.Pawn).Count());
        Assert.All(game.LegalDrops(PieceType.Pawn), move => Assert.InRange(move.To.Row, 1, 6));
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new DropMove(PieceType.Pawn, Player.White, new(0, 0))));
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new DropMove(PieceType.Pawn, Player.Black, new(3, 0))));
        Assert.Empty(Fen.Parse("k7/8/8/8/8/5n2/8/4K3[R] w - - 0 1").LegalDrops(PieceType.Rook));
    }

    [Fact]
    public void DroppedPawnMayDoubleStepFromHomeAndDroppedRookCannotCastle()
    {
        GameState pawn = Fen.Parse("4k3/8/8/8/8/8/8/4K3[p] b - - 0 1");
        Play(pawn, "@d7"); Play(pawn, "Kd1"); Play(pawn, "d5");
        Assert.Contains(" d6 ", pawn.ToFen());
        GameState rook = Fen.Parse("4k3/8/8/8/8/8/8/R3K3[R] w Q - 0 1");
        Play(rook, "R@h1"); Play(rook, "Kf8");
        Assert.DoesNotContain(rook.AllLegalMoves(Player.White), move => move.Type == MoveType.CastlingKS);
    }

    [Fact]
    public void EnPassantAddsAPawnAndCrazyhouseHasNoMoveCountOrMaterialDraw()
    {
        GameState game = Fen.Parse("4k3/8/8/3pP3/8/8/8/4K3[] w - d6 0 2");
        Play(game, "exd6"); Assert.Equal(1, game.Board.PocketCount(Player.White, PieceType.Pawn));
        GameState kings = Fen.Parse("4k3/8/8/8/8/8/8/4K3[] w - - 150 80");
        Assert.False(kings.IsGameOver()); Assert.False(kings.CanClaimDraw());
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/4K3[K] w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K~3[] w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3[P][q] w - - 0 1")]
    public void InvalidVariantPositionsAreRejected(string fen) => Assert.Throws<FormatException>(() => Fen.Parse(fen));

    [Fact]
    public void BotSearchUnderstandsReservesAndLeavesItsSourceUntouched()
    {
        GameState game = Fen.Parse("7k/8/5KQ1/8/8/8/8/8[R] w - - 0 1");
        string before = GameSave.Serialize(game);
        BotDecision decision = new ChessBot().ChooseMove(game, maxDepth: 2, nodeBudget: 500,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(before, GameSave.Serialize(game));
        game.ApplyMove(Assert.IsAssignableFrom<Move>(decision.Move));
        Assert.Equal(Player.White, game.Result!.Winner);
    }
}
