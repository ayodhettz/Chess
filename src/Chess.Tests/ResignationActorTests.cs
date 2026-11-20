using System.Text.Json.Nodes;
using ChessLogic;

namespace Chess.Tests;

public class ResignationActorTests
{
    [Theory]
    [InlineData(Player.White, Player.Black)]
    [InlineData(Player.Black, Player.White)]
    public void EitherSideCanResignWithoutChangingTheTurn(Player resigned, Player winner)
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        string fen = game.ToFen();
        game.Resign(resigned);
        Assert.Equal(Player.White, game.CurrentPlayer);
        Assert.Equal(winner, game.Result!.Winner);
        Assert.Equal(resigned, game.Result.ResignedBy);
        Assert.Equal(fen, game.ToFen());
        Assert.Empty(game.History);
        game.Undo(); Assert.Null(game.Result);
        game.Redo(); Assert.Equal(resigned, game.Result!.ResignedBy);
    }

    [Fact]
    public void OffTurnResignationAgainstABareKingIsADrawWithTheCorrectActor()
    {
        GameState game = Fen.Parse("k7/p7/8/8/8/8/8/7K w - - 0 1");
        game.Resign(Player.Black);
        Assert.Equal(Player.None, game.Result!.Winner);
        Assert.Equal(Player.Black, game.Result.ResignedBy);
        Assert.Equal(EndReason.Resignation, game.Result.Reason);
    }

    [Fact]
    public void VersionFourPreservesOffTurnActorsAndTheRedoTimeline()
    {
        GameState game = Fen.Parse(Fen.InitialPosition); game.Resign(Player.Black); game.Undo();
        string saved = GameSave.Serialize(game);
        Assert.Equal(4, JsonNode.Parse(saved)!["version"]!.GetValue<int>());
        GameState restored = GameSave.Deserialize(saved);
        Assert.Null(restored.Result);
        restored.Redo(); game.Redo();
        GameSaveTests.AssertEquivalent(game, restored);
        Assert.Equal(Player.Black, restored.Result!.ResignedBy);
        Assert.Equal(Player.White, restored.Result.Winner);
        Assert.Contains("[Result \"1-0\"]", Pgn.Format(restored));
    }

    [Fact]
    public void LegacyResignationsStillUseVersionOneAndRecoverTheActor()
    {
        GameState game = Fen.Parse(Fen.InitialPosition); game.Resign();
        string saved = GameSave.Serialize(game);
        Assert.Equal(1, JsonNode.Parse(saved)!["version"]!.GetValue<int>());
        Assert.Equal(Player.White, GameSave.Deserialize(saved).Result!.ResignedBy);
    }

    [Theory]
    [InlineData(3, "Black", "resign")]
    [InlineData(4, "None", "resign")]
    [InlineData(4, "Black", "offer")]
    public void ForgedOrUnsupportedActorsAreRejected(int version, string actor, string kind)
    {
        JsonNode save = JsonNode.Parse(GameSave.Serialize(Fen.Parse(Fen.InitialPosition)))!;
        save["version"] = version;
        save["actions"] = new JsonArray(new JsonObject { ["kind"] = kind, ["actor"] = actor });
        save["cursor"] = 1;
        Assert.Throws<FormatException>(() => GameSave.Deserialize(save.ToJsonString()));
    }
}
