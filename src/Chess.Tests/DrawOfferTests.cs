using System.Text.Json.Nodes;
using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class DrawOfferTests
{
    [Fact]
    public void PgnLimitsCountExportedMovesRatherThanOfferInteractions()
    {
        GameState game = Ready();
        for (int i = 0; i < 520; i++) { game.OfferDraw(); game.DeclineDraw(Player.Black); }
        Assert.Throws<FormatException>(() => GameSave.Serialize(game));
        Assert.Equal(game.ToFen(), Pgn.Parse(Pgn.Format(game)).ToFen());
    }

    [Fact]
    public void BlackCanOfferAndWhiteCanAcceptWithoutChangingTheTurn()
    {
        GameState game = Ready(); Play(game, "g1", "f3");
        game.OfferDraw();
        Assert.Equal(Player.Black, game.DrawOfferedBy);
        Assert.True(game.CanRespondToDrawOffer(Player.White));
        game.AcceptDraw(Player.White);
        Assert.Equal(EndReason.DrawAgreement, game.Result!.Reason);
        Assert.Equal(Player.Black, game.CurrentPlayer);
        Assert.Equal(3, game.History.Count);
    }

    [Fact]
    public void OffersAndDeclinesDoNotCountAsRepeatedPositionsOrMoves()
    {
        GameState game = Ready();
        string fen = game.ToFen();
        for (int i = 0; i < 4; i++) { game.OfferDraw(); game.DeclineDraw(Player.Black); }
        Assert.Equal(1, game.CurrentPositionRepetitions);
        Assert.False(game.CanClaimDraw());
        Assert.Equal(2, game.History.Count);
        Assert.Equal(fen, game.ToFen());
    }

    [Fact]
    public void OffersBecomeAvailableAfterBothPlayersHaveMoved()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Assert.False(game.CanOfferDraw);
        Assert.Throws<InvalidOperationException>(game.OfferDraw);
        Assert.False(game.CanUndo);
        Play(game, "e2", "e4");
        Assert.False(game.CanOfferDraw);
        Play(game, "e7", "e5");
        Assert.True(game.CanOfferDraw);
        game.OfferDraw();
        Assert.Equal(Player.White, game.DrawOfferedBy);
        Assert.Equal(Player.White, game.CurrentPlayer);
        Assert.Equal(2, game.History.Count);
        Assert.False(game.CanOfferDraw);
        Assert.Throws<InvalidOperationException>(game.OfferDraw);
    }

    [Fact]
    public void OnlyTheOpponentMayAcceptOrDecline()
    {
        GameState game = Ready();
        Assert.Throws<InvalidOperationException>(() => game.AcceptDraw(Player.Black));
        game.OfferDraw();
        Assert.True(game.CanRespondToDrawOffer(Player.Black));
        Assert.False(game.CanRespondToDrawOffer(Player.White));
        Assert.False(game.CanRespondToDrawOffer(Player.None));
        Assert.Throws<InvalidOperationException>(() => game.AcceptDraw(Player.White));
        Assert.Throws<InvalidOperationException>(() => game.DeclineDraw(Player.White));
        Assert.Equal(Player.White, game.DrawOfferedBy);
        game.AcceptDraw(Player.Black);
        Assert.Equal(Player.None, game.Result!.Winner);
        Assert.Equal(EndReason.DrawAgreement, game.Result.Reason);
        Assert.Null(game.DrawOfferedBy);
        Assert.Equal(2, game.History.Count);
        Assert.Throws<InvalidOperationException>(game.OfferDraw);
    }

    [Fact]
    public void DecliningLeavesTheTurnAndBoardUnchanged()
    {
        GameState game = Ready();
        string fen = game.ToFen();
        game.OfferDraw(); game.DeclineDraw(Player.Black);
        Assert.Null(game.Result);
        Assert.Null(game.DrawOfferedBy);
        Assert.Equal(fen, game.ToFen());
        Assert.True(game.CanOfferDraw);
    }

    [Fact]
    public void TheOfferSurvivesItsOwnersMoveAndTheOpponentsMoveDeclinesIt()
    {
        GameState game = Ready();
        game.OfferDraw();
        Play(game, "g1", "f3");
        Assert.Equal(Player.White, game.DrawOfferedBy);
        Assert.Equal(Player.Black, game.CurrentPlayer);
        Play(game, "g8", "f6");
        Assert.Null(game.DrawOfferedBy);
        Assert.Null(game.Result);
        game.Undo(); Assert.Equal(Player.White, game.DrawOfferedBy);
        game.Undo(); Assert.Equal(Player.White, game.DrawOfferedBy);
        game.Undo(); Assert.Null(game.DrawOfferedBy);
        game.Redo(); Assert.Equal(Player.White, game.DrawOfferedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResponsesAndTheirFutureSurviveUndoRedoAndSaving(bool accept)
    {
        GameState game = Ready();
        game.OfferDraw();
        if (accept) game.AcceptDraw(Player.Black); else game.DeclineDraw(Player.Black);
        game.Undo();
        Assert.Equal(Player.White, game.DrawOfferedBy);
        string text = GameSave.Serialize(game);
        Assert.Equal(3, JsonNode.Parse(text)!["version"]!.GetValue<int>());
        GameState loaded = GameSave.Deserialize(text);
        GameSaveTests.AssertEquivalent(game, loaded);
        loaded.Redo();
        Assert.Null(loaded.DrawOfferedBy);
        Assert.Equal(accept ? EndReason.DrawAgreement : (EndReason?)null, loaded.Result?.Reason);
        loaded.Undo(); Assert.Equal(Player.White, loaded.DrawOfferedBy);
    }

    [Fact]
    public void PendingOffersSurviveSavingAfterTheirOwnersMove()
    {
        GameState game = Ready();
        game.OfferDraw(); Play(game, "g1", "f3");
        GameState loaded = GameSave.Deserialize(GameSave.Serialize(game));
        GameSaveTests.AssertEquivalent(game, loaded);
        Assert.True(loaded.CanRespondToDrawOffer(Player.Black));
        loaded.AcceptDraw(Player.Black);
        Assert.Equal(EndReason.DrawAgreement, loaded.Result!.Reason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClaimsAndResignationClearOffersButUndoRestoresThem(bool resign)
    {
        GameState game = Fen.Parse(Fen.InitialPosition.Replace("0 1", "100 42"));
        game.OfferDraw();
        if (resign) game.Resign(); else game.ClaimDraw();
        Assert.Null(game.DrawOfferedBy);
        game.Undo();
        Assert.Equal(Player.White, game.DrawOfferedBy);
        Assert.Null(game.Result);
    }

    [Fact]
    public void CheckmateEndsAnOutstandingOffer()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "f2", "f3"); Play(game, "e7", "e5"); Play(game, "g2", "g4");
        game.OfferDraw(); Play(game, "d8", "h4");
        Assert.Equal(EndReason.Checkmate, game.Result!.Reason);
        Assert.Null(game.DrawOfferedBy);
        game.Undo(); Assert.Equal(Player.Black, game.DrawOfferedBy);
    }

    [Fact]
    public void IllegalMovesAndResponsesPreserveAnAvailableRedo()
    {
        GameState game = Ready();
        game.OfferDraw(); game.DeclineDraw(Player.Black); game.Undo();
        Assert.Throws<InvalidOperationException>(() => game.DeclineDraw(Player.White));
        Assert.Throws<ArgumentException>(() => game.ApplyMove(new NormalMove(At("e2"), At("e4"))));
        Assert.True(game.CanRedo);
        game.Redo(); Assert.Null(game.DrawOfferedBy);
    }

    [Fact]
    public void AcceptedOffersExportAsDrawsWhilePgnDoesNotPreserveOfferActions()
    {
        GameState game = Ready(); game.OfferDraw();
        Assert.Null(Pgn.Parse(Pgn.Format(game)).DrawOfferedBy);
        game.AcceptDraw(Player.Black);
        string pgn = Pgn.Format(game);
        Assert.Contains("[Result \"1/2-1/2\"]", pgn);
        Assert.Equal(Player.None, Pgn.Parse(pgn).Result!.Winner);
    }

    [Theory]
    [InlineData(1, "Black")]
    [InlineData(2, "Black")]
    [InlineData(3, "White")]
    [InlineData(3, "None")]
    public void UnsupportedOrForgedSavedResponsesAreRejected(int version, string responder)
    {
        GameState game = Ready(); game.OfferDraw();
        JsonNode document = JsonNode.Parse(GameSave.Serialize(game))!;
        document["version"] = version;
        document["actions"]!.AsArray().Add(new JsonObject { ["kind"] = "accept", ["responder"] = responder });
        document["cursor"] = game.ActionCursor + 1;
        Assert.Throws<FormatException>(() => GameSave.Deserialize(document.ToJsonString()));
    }

    [Fact]
    public void IntendedClaimsAreAvailableOnlyWhenALegalMoveQualifies()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Assert.False(game.CanClaimDrawWithIntendedMove());
        KnightCycle(game);
        Play(game, "g1", "f3"); Play(game, "g8", "f6"); Play(game, "f3", "g1");
        Assert.False(game.CanClaimDraw());
        Assert.True(game.CanClaimDrawWithIntendedMove());
        Assert.True(Fen.Parse(Fen.InitialPosition.Replace("0 1", "99 42")).CanClaimDrawWithIntendedMove());
        GameState forcedCapture = Fen.Parse("r7/8/8/8/8/2k5/8/Kr6 w - - 99 42");
        Assert.False(forcedCapture.CanClaimDrawWithIntendedMove());
    }

    private static GameState Ready()
    {
        GameState game = Fen.Parse(Fen.InitialPosition);
        Play(game, "e2", "e4"); Play(game, "e7", "e5");
        return game;
    }
}
