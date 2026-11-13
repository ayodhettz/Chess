using System.Globalization;
using System.Numerics;
using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class FenTests
{
    [Theory]
    [InlineData(Fen.InitialPosition)]
    [InlineData("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1")]
    [InlineData("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2")]
    [InlineData("rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2")]
    [InlineData("4k3/8/8/8/8/8/4P3/4K3 w - - 5 39")]
    [InlineData("k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 42")]
    [InlineData("7k/6Q1/6K1/8/8/8/8/8 b - - 150 82")]
    public void ValidPositionsRoundTripAllSixFields(string fen)
    {
        Assert.Equal(fen, GameState.FromFen(fen).ToFen());
    }

    [Fact]
    public void PlayedOpeningMatchesTheStandardExamples()
    {
        GameState game = new(Board.Initial(), Player.White);
        Assert.Equal(Fen.InitialPosition, game.ToFen());
        Play(game, "e2", "e4");
        Assert.Equal("e3", game.ToFen().Split(' ')[3]);
        Play(game, "c7", "c5");
        Assert.Equal("c6", game.ToFen().Split(' ')[3]);
        Play(game, "g1", "f3");
        Assert.Equal("rnbqkbnr/pp1ppppp/8/2p5/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2", game.ToFen());
    }

    public static IEnumerable<object[]> RightsCombinations()
    {
        for (int mask = 0; mask < 16; mask++)
        {
            string rights = new("KQkq".Where((_, index) => (mask & (1 << index)) != 0).ToArray());
            yield return new object[] { rights.Length == 0 ? "-" : rights };
        }
    }

    [Theory]
    [MemberData(nameof(RightsCombinations))]
    public void CastlingRightsSurviveEveryCombination(string rights)
    {
        string fen = $"r3k2r/8/8/8/8/8/8/R3K2R w {rights} - 0 1";
        GameState game = Fen.Parse(fen);
        Assert.Equal(fen, game.ToFen());
        Move[] kingMoves = game.LegalMoves(At("e1")).ToArray();
        Assert.Equal(rights.Contains('K'), kingMoves.Any(move => move.Type == MoveType.CastlingKS));
        Assert.Equal(rights.Contains('Q'), kingMoves.Any(move => move.Type == MoveType.CastlingQS));
    }

    [Fact]
    public void InputWhitespaceAndRightsOrderAreNormalized()
    {
        string fen = Fen.InitialPosition.Replace(" KQkq ", " qkQK ").Replace(" ", "\t");
        Assert.Equal(Fen.InitialPosition, Fen.Parse(" \n" + fen + "\r\n").ToFen());
    }

    [Fact]
    public void ImportedCastlingMovesBothPiecesAndRemovesWhiteRights()
    {
        GameState game = Fen.Parse("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 7 18");
        Play(game, "e1", "g1");
        Assert.IsType<King>(game.Board[At("g1")]);
        Assert.IsType<Rook>(game.Board[At("f1")]);
        Assert.Equal("kq", game.ToFen().Split(' ')[2]);
        Assert.Equal(8, game.HalfMoveClock);
    }

    [Fact]
    public void RookMovingAwayAndBackDoesNotRestoreRights()
    {
        GameState game = Fen.Parse("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        Play(game, "h1", "h2"); Play(game, "e8", "d8");
        Play(game, "h2", "h1");
        Assert.Equal("Q", game.ToFen().Split(' ')[2]);
    }

    [Theory]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 42", "e5", "d6", "d5")]
    [InlineData("4k3/8/8/8/3Pp3/8/8/4K3 b - d3 0 42", "e4", "d3", "d4")]
    public void ImportedEnPassantCanBePlayed(string fen, string from, string to, string captured)
    {
        GameState game = Fen.Parse(fen);
        Play(game, from, to);
        Assert.Null(game.Board[At(captured)]);
        Assert.Equal(MoveType.EnPassant, game.History[^1].MoveType);
        Assert.Equal("-", game.ToFen().Split(' ')[3]);
    }

    [Fact]
    public void PinnedEnPassantIsPreservedButNotOfferedAsLegal()
    {
        string fen = "k3r3/8/8/3pP3/8/8/8/4K3 w - d6 0 42";
        GameState game = Fen.Parse(fen);
        Assert.DoesNotContain(game.LegalMoves(At("e5")), move => move.Type == MoveType.EnPassant);
        Assert.Equal(fen, game.ToFen());
    }

    [Fact]
    public void StartingPawnsRetainTheirDoubleStep()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Assert.Contains(game.LegalMoves(At("e2")), move => move.Type == MoveType.DoubleStep);
    }

    [Fact]
    public void ImportedKiwipeteHasFortyEightLegalMoves()
    {
        GameState game = Fen.Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1");
        Assert.Equal(48, game.AllLegalMoves(Player.White).Count());
    }

    [Fact]
    public void FullMoveNumberAndHistoryAdvanceAfterBlackMoves()
    {
        GameState game = Fen.Parse(Fen.InitialPosition.Replace(" w ", " b ").Replace("0 1", "8 42"));
        Play(game, "g8", "f6");
        Assert.Equal(new BigInteger(42), game.History[^1].FullMoveNumber);
        Assert.Equal(new BigInteger(43), game.FullMoveNumber);
        Play(game, "g1", "f3");
        Assert.Equal(new BigInteger(43), game.History[^1].FullMoveNumber);
        Assert.Equal(new BigInteger(43), game.FullMoveNumber);
        Assert.Equal(10, game.HalfMoveClock);
    }

    [Fact]
    public void LargeFullMoveNumbersDoNotOverflow()
    {
        const string number = "999999999999999999999999999999";
        GameState game = Fen.Parse(Fen.InitialPosition.Replace(" w ", " b ").Replace("0 1", "0 " + number));
        Play(game, "g8", "f6");
        Assert.Equal(BigInteger.Parse(number) + 1, game.FullMoveNumber);
        Assert.EndsWith(" 1 1000000000000000000000000000000", game.ToFen());
    }

    [Fact]
    public void LoadingStartsFreshRepetitionHistory()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        KnightCycle(game); KnightCycle(game);
        Assert.True(game.CanClaimDraw());
        GameState loaded = Fen.Parse(game.ToFen());
        Assert.Empty(loaded.History);
        Assert.Equal(1, loaded.CurrentPositionRepetitions);
        Assert.False(loaded.CanClaimDraw());
    }

    [Theory]
    [InlineData(99, false, false)]
    [InlineData(100, true, false)]
    [InlineData(149, true, false)]
    [InlineData(150, false, true)]
    [InlineData(int.MaxValue, false, true)]
    public void ImportedHalfMoveClockControlsDrawEligibility(int clock, bool claimable, bool ended)
    {
        GameState game = Fen.Parse($"r3k3/8/8/8/8/8/8/R3K3 w - - {clock} 100");
        Assert.Equal(clock, game.HalfMoveClock);
        Assert.Equal(claimable, game.CanClaimDraw());
        Assert.Equal(ended, game.IsGameOver());
        if (ended) Assert.Equal(EndReason.SeventyFiveMoveRule, game.Result!.Reason);
    }

    [Fact]
    public void ImportedCheckmateTakesPrecedenceOverSeventyFiveMoves()
    {
        GameState game = Fen.Parse("7k/6Q1/6K1/8/8/8/8/8 b - - 150 82");
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("8/8/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1 extra")]
    [InlineData("4k3/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/3K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K4 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K30 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K12 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4X3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/3KK3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/P3K3 w - - 0 1")]
    [InlineData("P3k3/8/8/8/8/8/8/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/P7/PPPPPPPP/8/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 x - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w K - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K2R w KK - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K2R w A - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K2r w K - 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - e3 0 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - e6 0 1")]
    [InlineData("4k3/8/8/4P3/8/8/8/4K3 w - e6 0 1")]
    [InlineData("4k3/4n3/8/4p3/8/8/8/4K3 w - e6 0 1")]
    [InlineData("4k3/8/4N3/4p3/8/8/8/4K3 w - e6 0 1")]
    [InlineData("4k3/8/8/4p3/8/8/8/4K3 w - e6 1 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - -1 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 2147483648 1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 0")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 -1")]
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 +1")]
    [InlineData("8/8/8/8/8/8/4k3/4K3 w - - 0 1")]
    [InlineData("4k3/8/8/8/8/8/4R3/4K3 w - - 0 1")]
    public void InvalidInputReturnsAnErrorWithoutAGame(string fen)
    {
        Assert.False(Fen.TryParse(fen, out GameState? game, out string? error));
        Assert.Null(game);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.Throws<FormatException>(() => Fen.Parse(fen));
    }

    [Fact]
    public void NullAndOversizedInputHaveClearFailures()
    {
        Assert.Throws<ArgumentNullException>(() => Fen.Parse(null!));
        Assert.False(Fen.TryParse(null, out _, out _));
        Assert.False(Fen.TryParse(new string('8', 1025), out _, out _));
        Assert.Throws<ArgumentNullException>(() => Fen.Format(null!));
    }

    [Fact]
    public void ExportUsesInvariantNumbers()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            Assert.Equal(Fen.InitialPosition, Fen.Parse(Fen.InitialPosition).ToFen());
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
