using ChessLogic;
using static Chess.Tests.TestPositions;

namespace Chess.Tests;

public class PerftTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    public void InitialPositionMatchesKnownMoveCounts(int depth, long expected)
    {
        Assert.Equal(expected, Count(Board.Initial(), Player.White, depth));
    }

    [Theory]
    [InlineData(1, 48)]
    [InlineData(2, 2039)]
    public void KiwipeteCoversCastlingAndTacticalLegality(int depth, long expected)
    {
        Board board = Parse("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R", false);
        Assert.Equal(expected, Count(board, Player.White, depth));
    }

    private static long Count(Board board, Player player, int depth)
    {
        if (depth == 0) return 1;
        long nodes = 0;
        Move[] moves = board.PositionsOfPlayer(player)
            .SelectMany(pos => board.GetPiece(pos).GetMoves(pos, board))
            .Where(move => move.IsLegal(board)).ToArray();

        foreach (Move move in moves)
        {
            Board copy = board.Copy();
            copy.SetPawnSkipPosition(player, null!);
            move.Execute(copy);
            nodes += Count(copy, player.Opponent(), depth - 1);
        }
        return nodes;
    }
}
