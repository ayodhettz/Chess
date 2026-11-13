using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class SpecialMoveTests
{
    [Theory]
    [InlineData("e4", Player.White)]
    [InlineData("e5", Player.Black)]
    public void DoubleStepRequiresTheStartingRank(string from, Player player)
    {
        Board board = Parse("4k3/8/8/8/8/8/8/4K3", false);
        board[At(from)] = new Pawn(player);
        Assert.DoesNotContain(board.GetPiece(At(from)).GetMoves(At(from), board), move => move.Type == MoveType.DoubleStep);
    }

    [Fact]
    public void CastlingRequiresTheKingsStartingSquare()
    {
        Board board = Parse("k7/8/8/8/8/8/4K2R/8", false);
        Assert.DoesNotContain(board.GetPiece(At("e2")).GetMoves(At("e2"), board), move => move.Type == MoveType.CastlingKS);
    }

    [Theory]
    [InlineData("k4r2/8/8/8/8/8/8/4K2R")]
    [InlineData("k3r3/8/8/8/8/8/8/4K2R")]
    [InlineData("k5r1/8/8/8/8/8/8/4K2R")]
    public void CastlingCannotStartCrossOrEndInCheck(string placement)
    {
        GameState game = new(Parse(placement, false), Player.White);
        Assert.DoesNotContain(game.LegalMoves(At("e1")), move => move.Type == MoveType.CastlingKS);
    }

    [Theory]
    [InlineData("g1", "f1", "h1", MoveType.CastlingKS)]
    [InlineData("c1", "d1", "a1", MoveType.CastlingQS)]
    public void CastlingMovesBothPieces(string to, string rookTo, string rookFrom, MoveType type)
    {
        GameState game = new(Parse("4k3/8/8/8/8/8/8/R3K2R", false), Player.White);
        Play(game, "e1", to);
        Assert.IsType<King>(game.Board[At(to)]);
        Assert.IsType<Rook>(game.Board[At(rookTo)]);
        Assert.Null(game.Board[At(rookFrom)]);
        Assert.True(game.Board.GetPiece(At(to)).HasMoved);
        Assert.True(game.Board.GetPiece(At(rookTo)).HasMoved);
        Assert.Equal(type, game.History[^1].MoveType);
    }

    [Fact]
    public void OpponentRookCannotBeUsedToCastle()
    {
        Board board = Parse("k7/8/8/8/8/8/8/4K2r", false);
        Assert.DoesNotContain(board.GetPiece(At("e1")).GetMoves(At("e1"), board), move => move.Type == MoveType.CastlingKS);
    }

    [Fact]
    public void EnPassantCannotExposeTheKing()
    {
        Board board = Parse("k3r3/8/8/3pP3/8/8/8/4K3");
        board.SetPawnSkipPosition(Player.Black, At("d6"));
        GameState game = new(board, Player.White);
        Assert.DoesNotContain(game.LegalMoves(At("e5")), move => move.Type == MoveType.EnPassant);
    }

    [Theory]
    [InlineData(PieceType.Queen)]
    [InlineData(PieceType.Rook)]
    [InlineData(PieceType.Bishop)]
    [InlineData(PieceType.Knight)]
    public void PromotionSupportsEveryChoice(PieceType promotion)
    {
        GameState game = new(Parse("4k3/P7/8/8/8/8/8/4K3"), Player.White);
        Assert.Equal(4, game.LegalMoves(At("a7")).Count(move => move.To == At("a8")));
        Play(game, "a7", "a8", promotion);
        Assert.Equal(promotion, game.Board.GetPiece(At("a8")).Type);
        Assert.True(game.Board.GetPiece(At("a8")).HasMoved);
        Assert.Equal(promotion, game.History[^1].PromotionType);
        Assert.Equal(0, game.HalfMoveClock);
    }

    [Fact]
    public void PromotionCannotCreateAKing()
    {
        GameState game = new(Parse("4k3/P7/8/8/8/8/8/4K3"), Player.White);
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new PawnPromotion(At("a7"), At("a8"), PieceType.King)));
        Assert.IsType<Pawn>(game.Board[At("a7")]);
    }

    [Fact]
    public void PinnedRookCannotExposeItsKing()
    {
        GameState game = new(Parse("k3r3/8/8/8/8/8/4R3/4K3"), Player.White);
        Assert.DoesNotContain(game.LegalMoves(At("e2")), move => move.To == At("f2"));
        Assert.Contains(game.LegalMoves(At("e2")), move => move.To == At("e8"));
    }
}
