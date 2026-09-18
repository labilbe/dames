using Dames.Core;

namespace Dames.Core.Tests;

public class GameStateTests
{
    [Fact]
    public void MakeMove_RemovesEveryCapturedPieceAndPromotes()
    {
        var state = new GameState(Board.FromPositionString("W:12 B:8"), Player.White);
        Move move = Assert.Single(state.LegalMoves());

        state.MakeMove(move);

        Assert.Equal(Square.Empty, state.Board.AtNumber(12));
        Assert.Equal(Square.Empty, state.Board.AtNumber(8));
        Assert.Equal(Square.WhiteKing, state.Board.AtNumber(3));
        Assert.Equal(Player.Black, state.SideToMove);
    }

    [Fact]
    public void MakeMove_ThenUnmakeMove_RestoresBoardHashAndSideToMove()
    {
        var state = new GameState();
        string before = state.Board.Render();
        ulong hashBefore = state.Hash;

        var random = new Random(7);
        var played = new List<Move>();
        for (int i = 0; i < 12; i++)
        {
            List<Move> legal = state.LegalMoves();
            if (legal.Count == 0)
            {
                break;
            }

            Move move = legal[random.Next(legal.Count)];
            played.Add(move);
            state.MakeMove(move);
        }

        Assert.NotEqual(before, state.Board.Render());

        for (int i = 0; i < played.Count; i++)
        {
            state.UnmakeMove();
        }

        Assert.Equal(before, state.Board.Render());
        Assert.Equal(hashBefore, state.Hash);
        Assert.Equal(Player.White, state.SideToMove);
        Assert.Equal(0, state.PlyCount);
    }

    [Fact]
    public void Hash_IsConsistentWithAFullRecomputation()
    {
        var state = new GameState();
        var random = new Random(31);

        for (int i = 0; i < 20; i++)
        {
            List<Move> legal = state.LegalMoves();
            if (legal.Count == 0)
            {
                break;
            }

            state.MakeMove(legal[random.Next(legal.Count)]);
            Assert.Equal(Zobrist.Compute(state.Board, state.SideToMove), state.Hash);
        }
    }

    [Fact]
    public void PlayerWithoutAnyMove_LosesTheGame()
    {
        // Le pion blanc 46 est bloqué par 41 et la case d'arrivée 37 est occupée.
        var state = new GameState(Board.FromPositionString("W:46 B:41,37"), Player.White);

        Assert.Empty(state.LegalMoves());
        Assert.Equal(GameResult.BlackWins, state.Result());
    }

    [Fact]
    public void PlayerWithoutAnyPiece_LosesTheGame()
    {
        var state = new GameState(Board.FromPositionString("B:1,2"), Player.White);

        Assert.Equal(GameResult.BlackWins, state.Result());
    }

    [Fact]
    public void RepeatingTheSamePosition_IsADraw()
    {
        // Deux dames hors de portée l'une de l'autre : elles font la navette jusqu'à la triple répétition.
        var state = new GameState(Board.FromPositionString("W:K46 B:K15"), Player.White);

        foreach (string notation in (string[])["46-41", "15-20", "41-46", "20-15", "46-41", "15-20", "41-46", "20-15"])
        {
            Assert.Equal(GameResult.InProgress, state.Result());
            Assert.True(Notation.TryParse(notation, state.LegalMoves(), out Move? move, out string? error), error);
            state.MakeMove(move!);
        }

        Assert.Equal(GameResult.Draw, state.Result());
    }

    [Fact]
    public void QuietKingMoves_IncrementTheProgressCounterWhileManMovesResetIt()
    {
        var state = new GameState(Board.FromPositionString("W:K46,31 B:K15"), Player.White);

        Play(state, "46-41");
        Assert.Equal(1, state.PliesWithoutProgress);

        Play(state, "15-20");
        Assert.Equal(2, state.PliesWithoutProgress);

        Play(state, "31-27");
        Assert.Equal(0, state.PliesWithoutProgress);
    }

    private static void Play(GameState state, string notation)
    {
        Assert.True(Notation.TryParse(notation, state.LegalMoves(), out Move? move, out string? error), error);
        state.MakeMove(move!);
    }

    [Fact]
    public void CaptureResetsTheProgressCounter()
    {
        var state = new GameState(Board.FromPositionString("W:K46 B:23"), Player.White);
        state.MakeMove(state.LegalMoves().First());

        Assert.Equal(0, state.PliesWithoutProgress);
    }
}
