namespace Dames.Core;

public enum Difficulty
{
    /// <summary>Recherche très courte et choix volontairement imparfait.</summary>
    Easy,

    /// <summary>Quelques coups d'anticipation, sans erreur grossière.</summary>
    Medium,

    /// <summary>Recherche sérieuse limitée à deux secondes.</summary>
    Hard,

    /// <summary>Recherche profonde limitée à six secondes.</summary>
    Expert,
}

/// <summary>Paramètres de recherche de l'IA.</summary>
public sealed record SearchOptions
{
    public required int MaxDepth { get; init; }

    public required TimeSpan TimeLimit { get; init; }

    /// <summary>
    /// Marge, en centipions, à l'intérieur de laquelle un coup est jugé « assez bon » et
    /// peut être tiré au sort. Zéro rend l'IA déterministe.
    /// </summary>
    public int Randomness { get; init; }

    public int? RandomSeed { get; init; }

    public static SearchOptions For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => new SearchOptions
        {
            MaxDepth = 2,
            TimeLimit = TimeSpan.FromMilliseconds(200),
            Randomness = 120,
        },
        Difficulty.Medium => new SearchOptions
        {
            MaxDepth = 5,
            TimeLimit = TimeSpan.FromMilliseconds(800),
            Randomness = 30,
        },
        Difficulty.Hard => new SearchOptions
        {
            MaxDepth = 12,
            TimeLimit = TimeSpan.FromSeconds(2),
        },
        Difficulty.Expert => new SearchOptions
        {
            MaxDepth = 24,
            TimeLimit = TimeSpan.FromSeconds(6),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };

    public static string ToFrench(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Facile",
        Difficulty.Medium => "Moyen",
        Difficulty.Hard => "Difficile",
        Difficulty.Expert => "Expert",
        _ => difficulty.ToString(),
    };
}
