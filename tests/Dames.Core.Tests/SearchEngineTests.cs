using Dames.Core;

namespace Dames.Core.Tests;

public class SearchEngineTests
{
    [Fact]
    public void Search_ReturnsALegalMoveFromTheInitialPosition()
    {
        var state = new GameState();
        var engine = new SearchEngine(Difficulty.Medium);

        SearchResult result = engine.Search(state);

        Assert.NotNull(result.BestMove);
        Assert.Contains(state.LegalMoves(), m => m.Equals(result.BestMove));
        Assert.True(result.Nodes > 0);
    }

    [Fact]
    public void Search_LeavesTheGameStateUntouched()
    {
        var state = new GameState();
        string before = state.Board.Render();
        ulong hashBefore = state.Hash;

        new SearchEngine(Difficulty.Medium).Search(state);

        Assert.Equal(before, state.Board.Render());
        Assert.Equal(hashBefore, state.Hash);
        Assert.Equal(Player.White, state.SideToMove);
    }

    [Fact]
    public void Search_AvoidsHangingAManToAnObviousCapture()
    {
        // 32-28 et 31-27 abandonnent un pion au pion noir en 22 ; 32-27 et 31-26 le protègent.
        var state = new GameState(Board.FromPositionString("W:31,32 B:22"), Player.White);
        var engine = new SearchEngine(Difficulty.Hard);

        SearchResult result = engine.Search(state);

        Assert.NotNull(result.BestMove);
        Assert.DoesNotContain(result.BestMove!.ToShortNotation(), (string[])["32-28", "31-27"]);
    }

    [Fact]
    public void Search_FindsTheWinningRafle()
    {
        // Sacrifice 32-28 : quelle que soit la reprise noire (22x33 ou 23x32),
        // les Blancs enlèvent deux pions d'un coup (38x29x18 ou 38x27x18) et gagnent une pièce.
        var state = new GameState(Board.FromPositionString("W:32,38,42,43 B:22,23"), Player.White);
        var engine = new SearchEngine(Difficulty.Hard);

        SearchResult result = engine.Search(state);

        Assert.NotNull(result.BestMove);
        Assert.Equal("32-28", result.BestMove!.ToShortNotation());
    }

    [Fact]
    public void EasyDifficulty_IsReproducibleWithAFixedSeed()
    {
        SearchOptions options = SearchOptions.For(Difficulty.Easy) with { RandomSeed = 1234 };

        string First()
        {
            var state = new GameState();
            return new SearchEngine(options).Search(state).BestMove!.ToNotation();
        }

        Assert.Equal(First(), First());
    }

    [Fact]
    public void Engines_CanPlayAFullGameToCompletion()
    {
        var state = new GameState();
        var options = SearchOptions.For(Difficulty.Easy) with { RandomSeed = 99 };
        var engine = new SearchEngine(options);

        int plies = 0;
        while (!state.Result().IsOver() && plies < 400)
        {
            SearchResult result = engine.Search(state);
            Assert.NotNull(result.BestMove);
            state.MakeMove(result.BestMove!);
            plies++;
        }

        Assert.True(state.Result().IsOver(), $"Partie inachevée après {plies} demi-coups.");
    }
}
