using Dames.Core;

namespace Dames.Core.Tests;

public class MoveGeneratorTests
{
    private static List<Move> Moves(string position, Player player) =>
        MoveGenerator.Generate(Board.FromPositionString(position), player);

    private static string[] Notations(IEnumerable<Move> moves) =>
        [.. moves.Select(m => m.ToNotation()).Order()];

    [Fact]
    public void InitialPosition_HasTwentyMenPerSide()
    {
        Board board = Board.CreateInitial();

        Assert.Equal(20, board.Count(Square.WhiteMan));
        Assert.Equal(20, board.Count(Square.BlackMan));
        Assert.Equal(Square.BlackMan, board.AtNumber(20));
        Assert.Equal(Square.Empty, board.AtNumber(21));
        Assert.Equal(Square.Empty, board.AtNumber(30));
        Assert.Equal(Square.WhiteMan, board.AtNumber(31));
    }

    [Fact]
    public void InitialPosition_GivesNineMovesToEachSide()
    {
        Board board = Board.CreateInitial();

        Assert.Equal(9, MoveGenerator.Generate(board, Player.White).Count);
        Assert.Equal(9, MoveGenerator.Generate(board, Player.Black).Count);
    }

    [Fact]
    public void Man_CapturesForward()
    {
        List<Move> moves = Moves("W:32 B:28", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("32x23", move.ToNotation());
        Assert.Equal(1, move.CaptureCount);
    }

    [Fact]
    public void Man_CapturesBackwards()
    {
        // Aux dames internationales, le pion prend aussi en arrière.
        List<Move> moves = Moves("W:28 B:33", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("28x39", move.ToNotation());
    }

    [Fact]
    public void Capture_IsMandatory()
    {
        // Le pion 35 pourrait avancer tranquillement, mais une prise existe ailleurs.
        List<Move> moves = Moves("W:32,35 B:28", Player.White);

        Assert.All(moves, m => Assert.True(m.IsCapture));
        Assert.Equal(["32x23"], Notations(moves));
    }

    [Fact]
    public void MaximumCaptureRule_ForcesTheLongestRafle()
    {
        // 32 prend deux pièces, 35 n'en prend qu'une : seule la rafle maximale est légale.
        List<Move> moves = Moves("W:32,35 B:18,28,30", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("32x23x12", move.ToNotation());
        Assert.Equal(2, move.CaptureCount);
    }

    [Fact]
    public void King_SlidesAlongTheWholeDiagonal()
    {
        List<Move> moves = Moves("W:K46", Player.White);

        Assert.Equal(9, moves.Count);
        Assert.All(moves, m => Assert.False(m.IsCapture));
        Assert.Contains(moves, m => m.To == Squares.IndexOf(5));
    }

    [Fact]
    public void King_CapturesAtDistanceAndChoosesItsLandingSquare()
    {
        List<Move> moves = Moves("W:K46 B:23", Player.White);

        Assert.Equal(4, moves.Count);
        Assert.Equal(["46x10", "46x14", "46x19", "46x5"], Notations(moves));

        // La pièce déjà prise ne peut pas être reprise sur le chemin du retour.
        Assert.All(moves, m => Assert.Equal(1, m.CaptureCount));
    }

    [Fact]
    public void King_CannotJumpTwoPiecesInARow()
    {
        List<Move> moves = Moves("W:K46 B:32,28", Player.White);

        Assert.DoesNotContain(moves, m => m.IsCapture);
    }

    [Fact]
    public void Rafle_CannotTakeTheSamePieceTwiceAndMayReturnToItsStart()
    {
        // Boucle carrée : le pion prend quatre pièces et revient sur sa case de départ.
        List<Move> moves = Moves("W:33 B:18,19,28,29", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal(4, move.CaptureCount);
        Assert.Equal(Squares.IndexOf(33), move.To);
        Assert.Equal(4, move.Captures.Distinct().Count());
    }

    [Fact]
    public void CapturedPieces_StayOnTheBoardUntilTheRafleEnds()
    {
        // 46 prend 37 (arrivée forcée en 32, car 28 occupe la case suivante) puis prend 28.
        // Le retour est impossible : 37, déjà prise, reste sur le damier et bloque la diagonale.
        List<Move> moves = Moves("W:K46 B:37,28", Player.White);

        Assert.Equal(
            ["46x32x10", "46x32x14", "46x32x19", "46x32x23", "46x32x5"],
            Notations(moves));
        Assert.All(moves, m => Assert.Equal(2, m.CaptureCount));
    }

    [Fact]
    public void Man_PromotesWhenItStopsOnTheLastRow()
    {
        List<Move> moves = Moves("W:6", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("6-1", move.ToNotation());
        Assert.True(move.Promotes);
    }

    [Fact]
    public void Man_DoesNotPromoteWhenItOnlyCrossesTheLastRow()
    {
        // La rafle passe par la case 3 (dernière rangée) mais s'achève en 14 : pas de promotion.
        List<Move> moves = Moves("W:12 B:8,9", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("12x3x14", move.ToNotation());
        Assert.False(move.Promotes);
    }

    [Fact]
    public void Man_PromotesWhenTheRafleEndsOnTheLastRow()
    {
        List<Move> moves = Moves("W:12 B:8", Player.White);

        Move move = Assert.Single(moves);
        Assert.Equal("12x3", move.ToNotation());
        Assert.True(move.Promotes);
    }

    [Fact]
    public void Man_MovesOnlyForward()
    {
        List<Move> moves = Moves("W:28 B:1", Player.White);

        Assert.Equal(["28-22", "28-23"], Notations(moves));
    }

    [Fact]
    public void BlackMan_MovesTowardsTheBottom()
    {
        List<Move> moves = Moves("B:28 W:50", Player.Black);

        Assert.Equal(["28-32", "28-33"], Notations(moves));
    }
}
