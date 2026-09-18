using Dames.Core;

namespace Dames.Core.Tests;

/// <summary>
/// Comptage du nombre de parties distinctes à faible profondeur depuis la position initiale.
/// Ces valeurs sont les références publiées pour les dames internationales : toute erreur
/// dans les règles de prise ou de promotion les fait diverger immédiatement.
/// </summary>
public class PerftTests
{
    [Theory]
    [InlineData(1, 9)]
    [InlineData(2, 81)]
    [InlineData(3, 658)]
    [InlineData(4, 4265)]
    [InlineData(5, 27117)]
    public void Perft_MatchesTheReferenceCounts(int depth, long expected)
    {
        var state = new GameState();

        Assert.Equal(expected, Perft(state, depth));
    }

    private static long Perft(GameState state, int depth)
    {
        List<Move> moves = state.LegalMoves();
        if (depth <= 1)
        {
            return moves.Count;
        }

        long total = 0;
        foreach (Move move in moves)
        {
            state.MakeMove(move);
            total += Perft(state, depth - 1);
            state.UnmakeMove();
        }

        return total;
    }
}
