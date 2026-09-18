using System.Text.RegularExpressions;

namespace Dames.Core;

/// <summary>Lecture de la notation officielle saisie par un joueur ("32-28", "32x23", "32x23x34").</summary>
public static partial class Notation
{
    /// <summary>
    /// Résout une saisie en un coup de la liste des coups légaux.
    /// Les formes abrégées sont acceptées tant qu'elles ne sont pas ambiguës.
    /// </summary>
    public static bool TryParse(string input, IReadOnlyList<Move> legalMoves, out Move? move, out string? error)
    {
        move = null;
        error = null;

        int[] numbers = [.. NumberPattern().Matches(input ?? string.Empty).Select(m => int.Parse(m.Value))];

        if (numbers.Length < 2)
        {
            error = "Indiquez au moins deux cases, par exemple 32-28.";
            return false;
        }

        foreach (int number in numbers)
        {
            if (number is < 1 or > Squares.PlayableCount)
            {
                error = $"La case {number} n'existe pas : les cases vont de 1 à {Squares.PlayableCount}.";
                return false;
            }
        }

        int from = Squares.IndexOf(numbers[0]);
        int to = Squares.IndexOf(numbers[^1]);

        List<Move> candidates = [.. legalMoves.Where(m => m.From == from && m.To == to)];

        if (candidates.Count == 0)
        {
            error = $"Le coup {numbers[0]}-{numbers[^1]} n'est pas légal ici.";
            return false;
        }

        if (numbers.Length > 2)
        {
            int[] path = [.. numbers.Skip(1).Select(Squares.IndexOf)];
            candidates = [.. candidates.Where(m => m.Path.SequenceEqual(path))];

            if (candidates.Count == 0)
            {
                error = "Ce chemin de rafle n'est pas légal ici.";
                return false;
            }
        }

        if (candidates.Count > 1)
        {
            error = "Rafle ambiguë, précisez le chemin complet : " +
                    string.Join(", ", candidates.Select(c => c.ToNotation()));
            return false;
        }

        move = candidates[0];
        return true;
    }

    /// <summary>Description lisible d'un coup, par exemple "32x23 (2 pièces prises, promotion)".</summary>
    public static string Describe(Move move)
    {
        if (!move.IsCapture)
        {
            return move.Promotes ? $"{move.ToShortNotation()} (promotion)" : move.ToShortNotation();
        }

        string pieces = move.CaptureCount == 1 ? "1 pièce prise" : $"{move.CaptureCount} pièces prises";
        return move.Promotes
            ? $"{move.ToNotation()} ({pieces}, promotion)"
            : $"{move.ToNotation()} ({pieces})";
    }

    [GeneratedRegex(@"\d{1,2}")]
    private static partial Regex NumberPattern();
}
