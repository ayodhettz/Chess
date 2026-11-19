using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class SanTests
{
    [Theory]
    [InlineData(Fen.InitialPosition, "e2", "e4", null, "e4")]
    [InlineData(Fen.InitialPosition, "g1", "f3", null, "Nf3")]
    [InlineData("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1", "g1", null, "O-O")]
    [InlineData("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", "e1", "c1", null, "O-O-O")]
    [InlineData("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1", "e4", "d5", null, "exd5")]
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 42", "e5", "d6", null, "exd6")]
    [InlineData("4k3/8/8/8/8/8/r7/R3K3 w - - 0 1", "a1", "a2", null, "Rxa2")]
    [InlineData("4k3/8/8/8/8/2N5/8/4K1N1 w - - 0 1", "c3", "e2", null, "Nce2")]
    [InlineData("4k3/8/8/8/8/2N5/8/4K1N1 w - - 0 1", "g1", "e2", null, "Nge2")]
    [InlineData("4k3/8/8/8/1b6/2N5/8/4K1N1 w - - 0 1", "g1", "e2", null, "Ne2")]
    [InlineData("7k/8/8/8/8/N7/8/N6K w - - 0 1", "a1", "c2", null, "N1c2")]
    [InlineData("7k/8/8/8/8/N7/8/N6K w - - 0 1", "a3", "c2", null, "N3c2")]
    [InlineData("7k/8/8/1N6/8/1N3N2/8/7K w - - 0 1", "b3", "d4", null, "Nb3d4")]
    [InlineData("7k/8/8/1N6/8/1N3N2/8/7K w - - 0 1", "b5", "d4", null, "N5d4")]
    [InlineData("7k/8/8/1N6/8/1N3N2/8/7K w - - 0 1", "f3", "d4", null, "Nfd4")]
    [InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7", "a8", PieceType.Queen, "a8=Q+")]
    [InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7", "a8", PieceType.Rook, "a8=R+")]
    [InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7", "a8", PieceType.Bishop, "a8=B")]
    [InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7", "a8", PieceType.Knight, "a8=N")]
    [InlineData("1r2k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7", "b8", PieceType.Knight, "axb8=N")]
    [InlineData("7k/8/6K1/5Q2/8/8/8/8 w - - 0 1", "f5", "f7", null, "Qf7")]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 149 80", "a1", "a8", null, "Ra8+")]
    public void FormatsCanonicalSanAndParsesItWithoutMutatingTheGame(string fen, string from, string to, PieceType? promotion, string san)
    {
        GameState game = Fen.Parse(fen);
        Move move = FindMove(game, from, to, promotion);
        Assert.Equal(san, San.Format(game, move));
        Move parsed = San.Parse(game, san);
        Assert.Equal(move.From, parsed.From);
        Assert.Equal(move.To, parsed.To);
        Assert.Equal(move.Type, parsed.Type);
        Assert.Equal(fen, game.ToFen());
        Assert.False(game.CanUndo);
        game.ApplyMove(parsed);
        Assert.Equal(san, game.History[^1].San);
    }

    [Fact]
    public void MateSuffixAndCommonImportAnnotationsAreHandled()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "f2", "f3"); Play(game, "e7", "e5"); Play(game, "g2", "g4");
        Assert.Equal("Qh4#", San.Format(game, FindMove(game, "d8", "h4")));
        Assert.Equal(At("h4"), San.Parse(game, "Qh4#?!").To);
        Assert.Equal(At("h4"), San.Parse(game, "Qh4").To);
        Assert.Throws<FormatException>(() => San.Parse(game, "Qh4+"));
    }

    [Fact]
    public void ZeroCastlingIsAcceptedButCanonicalOutputUsesLetters()
    {
        GameState game = Fen.Parse("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
        Move move = San.Parse(game, "0-0");
        Assert.Equal("O-O", San.Format(game, move));
    }

    [Theory]
    [InlineData("e5")]
    [InlineData("e4+")]
    [InlineData("e4!!!")]
    [InlineData("Ng1-f3")]
    [InlineData("")]
    public void IllegalOrUnsupportedSanIsRejected(string text)
    {
        Assert.Throws<FormatException>(() => San.Parse(Fen.Parse(Fen.InitialPosition), text));
    }

    [Fact]
    public void MissingDisambiguationIsRejected()
    {
        GameState game = Fen.Parse("4k3/8/8/8/8/2N5/8/4K1N1 w - - 0 1");
        Assert.Throws<FormatException>(() => San.Parse(game, "Ne2"));
    }
}
