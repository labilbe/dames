namespace Dames.Core;

public enum GameResult
{
    InProgress,
    WhiteWins,
    BlackWins,
    Draw,
}

public static class GameResultExtensions
{
    public static bool IsOver(this GameResult result) => result != GameResult.InProgress;

    public static string ToFrench(this GameResult result) => result switch
    {
        GameResult.InProgress => "partie en cours",
        GameResult.WhiteWins => "les Blancs gagnent",
        GameResult.BlackWins => "les Noirs gagnent",
        GameResult.Draw => "partie nulle",
        _ => "inconnu",
    };
}
