using Dames.Core;

namespace Dames.Core.Tests;

public class NotationTests
{
    private static List<Move> Legal(string position, Player player) =>
        MoveGenerator.Generate(Board.FromPositionString(position), player);

    [Theory]
    [InlineData("32-28")]
    [InlineData("32 28")]
    [InlineData("32x28")]
    public void TryParse_AcceptsTheUsualSeparators(string input)
    {
        Assert.True(Notation.TryParse(input, Legal("W:31-50 B:1-20", Player.White), out Move? move, out _));
        Assert.Equal("32-28", move!.ToShortNotation());
    }

    [Fact]
    public void TryParse_AcceptsAFullRaflePath()
    {
        List<Move> legal = Legal("W:32 B:18,28", Player.White);

        Assert.True(Notation.TryParse("32x23x12", legal, out Move? move, out string? error), error);
        Assert.Equal(2, move!.CaptureCount);
    }

    [Fact]
    public void TryParse_RejectsAnIllegalMove()
    {
        Assert.False(Notation.TryParse("32-27", Legal("W:32 B:28", Player.White), out _, out string? error));
        Assert.Contains("pas légal", error);
    }

    [Fact]
    public void TryParse_RejectsAnOutOfRangeSquare()
    {
        Assert.False(Notation.TryParse("32-99", Legal("W:31-50 B:1-20", Player.White), out _, out string? error));
        Assert.Contains("n'existe pas", error);
    }

    [Fact]
    public void TryParse_AcceptsTheShortFormWhenItIsUnambiguous()
    {
        // Une seule rafle mène de 46 à 5 : inutile de détailler le chemin.
        List<Move> legal = Legal("W:K46 B:37,28", Player.White);

        Assert.True(Notation.TryParse("46x5", legal, out Move? move, out string? error), error);
        Assert.Equal("46x32x5", move!.ToNotation());
    }

    [Fact]
    public void Describe_MentionsCapturesAndPromotion()
    {
        Move promotion = Assert.Single(Legal("W:12 B:8", Player.White));

        Assert.Equal("12x3 (1 pièce prise, promotion)", Notation.Describe(promotion));
    }
}
