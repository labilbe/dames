using Dames.Core;

namespace Dames.Core.Tests;

public class SquaresTests
{
    [Fact]
    public void Numbering_RoundTripsForEveryPlayableSquare()
    {
        for (int number = 1; number <= Squares.PlayableCount; number++)
        {
            int index = Squares.IndexOf(number);
            Assert.True(Squares.IsPlayable(Squares.Row(index), Squares.Col(index)));
            Assert.Equal(number, Squares.NumberOf(index));
        }
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(5, 0, 9)]
    [InlineData(6, 1, 0)]
    [InlineData(28, 5, 4)]
    [InlineData(32, 6, 3)]
    [InlineData(46, 9, 0)]
    [InlineData(50, 9, 8)]
    public void Numbering_MatchesTheOfficialLayout(int number, int row, int col)
    {
        Assert.Equal(Squares.Index(row, col), Squares.IndexOf(number));
    }

    [Fact]
    public void LightSquares_AreNotPlayable()
    {
        Assert.False(Squares.IsPlayable(0, 0));
        Assert.False(Squares.IsPlayable(9, 9));
        Assert.Equal(0, Squares.NumberOf(Squares.Index(0, 0)));
    }
}
