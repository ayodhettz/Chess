using System.Text.Json.Nodes;
using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class PgnTests
{
    private const string Opera = "1. e4 e5 2. Nf3 d6 3. d4 Bg4 4. dxe5 Bxf3 5. Qxf3 dxe5 6. Bc4 Nf6 7. Qb3 Qe7 8. Nc3 c6 9. Bg5 b5 10. Nxb5 cxb5 11. Bxb5+ Nbd7 12. O-O-O Rd8 13. Rxd7 Rxd7 14. Rd1 Qe6 15. Bxd7+ Nxd7 16. Qb8+ Nxb8 17. Rd8# 1-0";

    [Theory]
    [InlineData("1.e4 e5 2.Nf3 Nc6 *", 4)]
    [InlineData("1. f3 e5 2. g4 Qh4# 0-1", 4)]
    [InlineData("1. e4 e5 2. Qh5 Nc6 3. Bc4 Nf6 4. Qxf7# 1-0", 7)]
    [InlineData(Opera, 33)]
    public void GamesImportAndExportWithLegalSan(string text, int plies)
    {
        GameState game = Pgn.Parse(text);
        Assert.Equal(plies, game.History.Count);
        string exported = Pgn.Format(game);
        Assert.Contains("[Event \"?\"]", exported);
        Assert.DoesNotContain("e2-e4", exported);
        GameState loaded = Pgn.Parse(exported);
        Assert.Equal(game.ToFen(), loaded.ToFen());
        Assert.Equal(game.Result?.Winner, loaded.Result?.Winner);
        Assert.Equal(game.History.Select(move => move.San), loaded.History.Select(move => move.San));
        Assert.All(exported.Split('\n'), line => Assert.True(line.Length <= 80));
    }

    [Fact]
    public void CommentsNagsNestedVariationsAndEscapedLinesDoNotChangeTheMainLine()
    {
        const string text = "% ignored line\n1.e4!?$1 { comment (not a variation) } (1.d4 d5 (1...Nf6)) e5 ; end comment\n2.Nf3 Nc6 *";
        GameState game = Pgn.Parse(text);
        Assert.Equal(new[] { "e4", "e5", "Nf3", "Nc6" }, game.History.Select(move => move.San));
        Assert.Equal(Pgn.Parse("1.e4 e5 2.Nf3 Nc6 *").ToFen(), game.ToFen());
    }

    [Fact]
    public void HeaderEscapesAndMetadataSurviveExportAndCompleteSaves()
    {
        GameState game = Pgn.Parse("[Event \"A \\\"quote\\\" and \\\\ path\"]\n[White \"Alice\"]\n[Black \"Bob\"]\n1.e4 e5 1/2-1/2");
        Assert.Equal("A \"quote\" and \\ path", game.PgnTags["Event"]);
        Assert.Equal(EndReason.RecordedResult, game.Result!.Reason);
        GameState parsed = Pgn.Parse(Pgn.Format(game));
        Assert.Equal(game.PgnTags["Event"], parsed.PgnTags["Event"]);
        string save = GameSave.Serialize(game);
        Assert.Equal(2, JsonNode.Parse(save)!["version"]!.GetValue<int>());
        GameState loaded = GameSave.Deserialize(save);
        GameSaveTests.AssertEquivalent(game, loaded);
        Assert.Equal("Alice", loaded.PgnTags["White"]);
        loaded.Undo();
        Assert.Null(loaded.Result);
        loaded.Redo();
        Assert.Equal(EndReason.RecordedResult, loaded.Result!.Reason);
    }

    [Theory]
    [InlineData("42...Nf6 43.Nf3 *")]
    [InlineData("42. ... Nf6 43. Nf3 *")]
    public void CustomStartingPositionsUseSetupFenAndBlackMoveNumbers(string movetext)
    {
        string fen = Fen.InitialPosition.Replace(" w ", " b ").Replace("0 1", "8 42");
        GameState game = Pgn.Parse($"[SetUp \"1\"]\n[FEN \"{fen}\"]\n{movetext}");
        Assert.Equal(fen, game.InitialFen);
        string text = Pgn.Format(game);
        Assert.Contains("[SetUp \"1\"]", text);
        Assert.Contains("42... Nf6 43. Nf3 *", text);
        Assert.Equal(game.ToFen(), Pgn.Parse(text).ToFen());
    }

    [Fact]
    public void ExportUsesTheCurrentTimelineAndResultRatherThanAnOldResultTag()
    {
        GameState game = Pgn.Parse("[Result \"0-1\"] 1.f3 e5 2.g4 Qh4# 0-1");
        game.Undo();
        string text = Pgn.Format(game);
        Assert.Contains("[Result \"*\"]", text);
        Assert.DoesNotContain("Qh4", text);
        GameState loaded = Pgn.Parse(text);
        Assert.Equal(game.ToFen(), loaded.ToFen());
        Assert.False(loaded.CanRedo);
        Assert.Null(loaded.Result);
    }

    [Theory]
    [InlineData("1-0", Player.White)]
    [InlineData("0-1", Player.Black)]
    [InlineData("1/2-1/2", Player.None)]
    public void RecordedResultsAreUndoableWithoutInventingAResignation(string marker, Player winner)
    {
        GameState game = Pgn.Parse("1.e4 " + marker);
        Assert.Equal(winner, game.Result!.Winner);
        Assert.Equal(EndReason.RecordedResult, game.Result.Reason);
        game.Undo(); Assert.Null(game.Result); Assert.Single(game.History);
        game.Redo(); Assert.Equal(winner, game.Result!.Winner);
    }

    [Fact]
    public void EnPassantAnnotationAndAttachedUnfinishedMarkerAreAccepted()
    {
        GameState game = Pgn.Parse("1.e4 a6 2.e5 d5 3.exd6 e.p.*");
        Assert.Equal("exd6", game.History[^1].San);
        Assert.Equal(MoveType.EnPassant, game.History[^1].MoveType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.e4")]
    [InlineData("1.e5 *")]
    [InlineData("2.e4 *")]
    [InlineData("[Result \"1-0\"] 1.e4 0-1")]
    [InlineData("1.f3 e5 2.g4 Qh4# *")]
    [InlineData("1.f3 e5 2.g4 Qh4# 1-0")]
    [InlineData("1.e4 * 1.d4 *")]
    [InlineData("1.e4 * [Event \"second\"] 1.d4 *")]
    [InlineData("1.e4 { open *")]
    [InlineData("1.e4 (1.d4 *")]
    [InlineData("1.e4 ) *")]
    [InlineData("1.e4 $256 *")]
    [InlineData("1.e4 e.p. *")]
    [InlineData("[Event \"a\"] [Event \"b\"] *")]
    [InlineData("[Event \"open] *")]
    [InlineData("[Event \"bad\\n\"] *")]
    [InlineData("[SetUp \"1\"] *")]
    [InlineData("[FEN \"4k3/8/8/8/8/8/8/4K3 w - - 0 1\"] *")]
    [InlineData("[Variant \"Chess960\"] *")]
    [InlineData("[Result \"unknown\"] *")]
    [InlineData("1.e4 *\0ignored")]
    public void InvalidOrAmbiguousGamesAreRejected(string text)
    {
        Assert.Throws<FormatException>(() => Pgn.Parse(text));
    }

    [Fact]
    public void OldVersionOneSavesStillLoadAndUnsafeMetadataIsRejected()
    {
        string text = $"{{\"version\":1,\"initialFen\":\"{Fen.InitialPosition}\",\"actions\":[],\"cursor\":0}}";
        Assert.Equal(Fen.InitialPosition, GameSave.Deserialize(text).ToFen());
        JsonNode document = JsonNode.Parse(text)!;
        document["version"] = 2;
        document["tags"] = new JsonObject { ["Event"] = "bad\nheader" };
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void PgnFilesCanBeWrittenAndRead()
    {
        using TempGameFolder folder = new();
        GameState game = Pgn.Parse(Opera);
        Pgn.Save(folder.File, game);
        Assert.Equal(game.ToFen(), Pgn.Load(folder.File).ToFen());
    }

    [Fact]
    public void SizeAndVariationDepthLimitsAreEnforced()
    {
        Assert.Throws<FormatException>(() => Pgn.Parse(new string(' ', GameSave.MaxCharacters + 1)));
        Assert.Throws<FormatException>(() => Pgn.Parse("1.e4 " + new string('(', 65) + new string(')', 65) + " *"));
    }

    [Theory]
    [InlineData(1, "result", "White")]
    [InlineData(2, "move", "White")]
    [InlineData(2, "result", "Unknown")]
    public void SaveFilesCannotSmuggleUnsupportedResultActions(int version, string kind, string winner)
    {
        JsonNode document = JsonNode.Parse(GameSave.Serialize(Fen.Parse(Fen.InitialPosition)))!;
        document["version"] = version;
        document["actions"]!.AsArray().Add(new JsonObject { ["kind"] = kind, ["winner"] = winner });
        document["cursor"] = 1;
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }
}
