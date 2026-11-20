using System.Text.Json.Nodes;
using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class GameSaveTests
{
    [Fact]
    public void PlayedCheckmateAndItsPreviousPositionSurvive()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "f2", "f3"); Play(game, "e7", "e5"); Play(game, "g2", "g4"); Play(game, "d8", "h4");
        GameState loaded = GameSave.Deserialize(GameSave.Serialize(game));
        AssertEquivalent(game, loaded);
        game.Undo(); loaded.Undo(); AssertEquivalent(game, loaded);
    }

    [Fact]
    public void SaveRestoresBothSidesOfTheUndoRedoTimeline()
    {
        GameState original = Fen.Parse(Fen.InitialPosition);
        KnightCycle(original); KnightCycle(original); original.ClaimDraw();
        original.Undo(); original.Undo();
        string text = GameSave.Serialize(original);
        GameState loaded = GameSave.Deserialize(text);
        Assert.Equal(text, GameSave.Serialize(loaded));
        AssertEquivalent(original, loaded);
        while (original.CanRedo) { original.Redo(); loaded.Redo(); AssertEquivalent(original, loaded); }
        while (original.CanUndo) { original.Undo(); loaded.Undo(); AssertEquivalent(original, loaded); }
    }

    [Theory]
    [MemberData(nameof(UndoRedoTests.SpecialMoves), MemberType = typeof(UndoRedoTests))]
    public void SaveRestoresSpecialMovesAndTheirRedo(string fen, string from, string to, PieceType? promotion)
    {
        GameState original = Fen.Parse(fen);
        Play(original, from, to, promotion);
        GameState loaded = GameSave.Deserialize(GameSave.Serialize(original));
        AssertEquivalent(original, loaded);
        original.Undo(); loaded.Undo(); AssertEquivalent(original, loaded);
        GameState withFuture = GameSave.Deserialize(GameSave.Serialize(loaded));
        original.Redo(); withFuture.Redo(); AssertEquivalent(original, withFuture);
    }

    [Fact]
    public void ResignationAndIntendedMoveClaimsArePreserved()
    {
        GameState resignation = Fen.Parse(Fen.InitialPosition);
        Play(resignation, "e2", "e4"); resignation.Resign();
        AssertEquivalent(resignation, GameSave.Deserialize(GameSave.Serialize(resignation)));
        GameState claim = Fen.Parse(Fen.InitialPosition);
        KnightCycle(claim);
        Play(claim, "g1", "f3"); Play(claim, "g8", "f6"); Play(claim, "f3", "g1");
        claim.ClaimDraw(FindMove(claim, "f6", "g8"));
        GameState loaded = GameSave.Deserialize(GameSave.Serialize(claim));
        AssertEquivalent(claim, loaded);
        Assert.Equal(7, loaded.History.Count);
    }

    [Fact]
    public void ANewBranchDoesNotSaveTheAbandonedFuture()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4"); Play(game, "e7", "e5"); game.Undo(); Play(game, "c7", "c5");
        GameState loaded = GameSave.Deserialize(GameSave.Serialize(game));
        AssertEquivalent(game, loaded);
        Assert.False(loaded.CanRedo);
        Assert.DoesNotContain("e7", GameSave.Serialize(game));
    }

    [Theory]
    [InlineData(Fen.InitialPosition)]
    [InlineData("r3k3/8/8/8/8/8/8/R3K3 b - - 149 42")]
    [InlineData("7k/6Q1/6K1/8/8/8/8/8 b - - 150 82")]
    public void StartingPositionCountersAndTerminalPositionsSurvive(string fen)
    {
        GameState game = Fen.Parse(fen);
        AssertEquivalent(game, GameSave.Deserialize(GameSave.Serialize(game)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("{\"version\":99,\"initialFen\":\"x\",\"actions\":[],\"cursor\":0}")]
    [InlineData("{\"version\":1,\"initialFen\":null,\"actions\":[],\"cursor\":0}")]
    public void MalformedOrUnsupportedDocumentsAreRejected(string text)
    {
        Assert.Throws<FormatException>(() => GameSave.Deserialize(text));
    }

    [Theory]
    [InlineData("cursor", -1)]
    [InlineData("cursor", 1)]
    [InlineData("unknown", 1)]
    public void InvalidCursorAndUnknownFieldsAreRejected(string key, int value)
    {
        JsonNode document = JsonNode.Parse(GameSave.Serialize(Fen.Parse(Fen.InitialPosition)))!;
        document[key] = value;
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }

    [Theory]
    [InlineData("move", "e2", "e5", null)]
    [InlineData("move", "z2", "e4", null)]
    [InlineData("move", "e7", "e5", null)]
    [InlineData("move", "e2", "e4", "King")]
    [InlineData("move", "e2", "e4", "Unknown")]
    [InlineData("resign", "e2", "e4", null)]
    [InlineData("claim", null, null, null)]
    [InlineData("unknown", null, null, null)]
    public void EvenUndoneFutureActionsMustBeValid(string kind, string? from, string? to, string? promotion)
    {
        JsonNode document = JsonNode.Parse(GameSave.Serialize(Fen.Parse(Fen.InitialPosition)))!;
        document["actions"]!.AsArray().Add(new JsonObject
            { ["kind"] = kind, ["from"] = from, ["to"] = to, ["promotion"] = promotion });
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void SizeAndActionLimitsAreEnforced()
    {
        Assert.Throws<FormatException>(() => GameSave.Deserialize(new string(' ', GameSave.MaxCharacters + 1)));
        JsonNode document = JsonNode.Parse(GameSave.Serialize(Fen.Parse(Fen.InitialPosition)))!;
        for (int i = 0; i <= GameSave.MaxActions; i++) document["actions"]!.AsArray().Add(new JsonObject { ["kind"] = "resign" });
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }

    internal static void AssertEquivalent(GameState expected, GameState actual)
    {
        Assert.Equal(expected.InitialFen, actual.InitialFen);
        Assert.Equal(expected.ToFen(), actual.ToFen());
        Assert.Equal(expected.Result?.Reason, actual.Result?.Reason);
        Assert.Equal(expected.Result?.Winner, actual.Result?.Winner);
        Assert.Equal(expected.Result?.ResignedBy, actual.Result?.ResignedBy);
        Assert.Equal(expected.CurrentPositionRepetitions, actual.CurrentPositionRepetitions);
        Assert.Equal(expected.CanClaimDraw(), actual.CanClaimDraw());
        Assert.Equal(expected.CanUndo, actual.CanUndo);
        Assert.Equal(expected.CanRedo, actual.CanRedo);
        Assert.Equal(expected.DrawOfferedBy, actual.DrawOfferedBy);
        Assert.Equal(expected.History.Select(Describe), actual.History.Select(Describe));
    }

    private static string Describe(MoveRecord move) => $"{move.Player}/{move.FullMoveNumber}/{move.PieceType}/{move.MoveType}/{move}/{move.CapturedPieceType}/{move.PromotionType}/{move.San}";
}
