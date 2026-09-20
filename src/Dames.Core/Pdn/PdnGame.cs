namespace Dames.Core.Pdn;

/// <summary>Une erreur de lecture d'un fichier PDN.</summary>
public sealed class PdnException : Exception
{
    public PdnException(string message)
        : base(message)
    {
    }
}

/// <summary>Une en-tête PDN, par exemple <c>[Event "Championnat"]</c>.</summary>
public readonly record struct PdnTag(string Name, string Value);

/// <summary>
/// Une partie lue depuis un fichier PDN : ses en-têtes, sa position de départ et ses coups,
/// déjà validés contre les règles.
/// </summary>
public sealed class PdnGame
{
    internal PdnGame(
        IReadOnlyList<PdnTag> tags,
        Board startingBoard,
        Player startingSide,
        IReadOnlyList<Move> moves,
        GameResult result)
    {
        Tags = tags;
        StartingBoard = startingBoard;
        StartingSide = startingSide;
        Moves = moves;
        Result = result;
    }

    public IReadOnlyList<PdnTag> Tags { get; }

    public Board StartingBoard { get; }

    public Player StartingSide { get; }

    /// <summary>Les coups de la partie, résolus en coups légaux.</summary>
    public IReadOnlyList<Move> Moves { get; }

    /// <summary>Le résultat déclaré par l'en-tête <c>Result</c>.</summary>
    public GameResult Result { get; }

    public string? Tag(string name) =>
        Tags.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) is { Name: not null } tag
            ? tag.Value
            : null;

    /// <summary>Rejoue la partie et renvoie l'état obtenu.</summary>
    public GameState ToGameState()
    {
        var state = new GameState(StartingBoard, StartingSide);
        foreach (Move move in Moves)
        {
            state.MakeMove(move);
        }

        return state;
    }
}
