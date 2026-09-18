using System.Text;

namespace Dames.Core;

/// <summary>
/// Un coup complet : un déplacement simple, ou une rafle entière avec toutes ses prises.
/// Les index sont des index internes de damier (voir <see cref="Squares"/>).
/// </summary>
public sealed class Move : IEquatable<Move>
{
    public Move(int from, IReadOnlyList<int> path, IReadOnlyList<int> captures, bool promotes)
    {
        From = from;
        Path = path;
        Captures = captures;
        Promotes = promotes;
    }

    /// <summary>Case de départ.</summary>
    public int From { get; }

    /// <summary>Cases d'arrivée successives ; la dernière est la case finale.</summary>
    public IReadOnlyList<int> Path { get; }

    /// <summary>Cases des pièces capturées, dans l'ordre de la rafle.</summary>
    public IReadOnlyList<int> Captures { get; }

    /// <summary>Vrai si le pion est promu en dame à l'issue du coup.</summary>
    public bool Promotes { get; }

    public int To => Path[^1];

    public bool IsCapture => Captures.Count > 0;

    public int CaptureCount => Captures.Count;

    /// <summary>Notation officielle abrégée : "32-28" ou "32x23".</summary>
    public string ToShortNotation() =>
        $"{Squares.NumberOf(From)}{(IsCapture ? 'x' : '-')}{Squares.NumberOf(To)}";

    /// <summary>Notation détaillée listant toutes les cases traversées : "32x23x34".</summary>
    public string ToNotation()
    {
        if (!IsCapture)
        {
            return ToShortNotation();
        }

        var sb = new StringBuilder();
        sb.Append(Squares.NumberOf(From));
        foreach (int step in Path)
        {
            sb.Append('x').Append(Squares.NumberOf(step));
        }

        return sb.ToString();
    }

    public bool Equals(Move? other)
    {
        if (other is null)
        {
            return false;
        }

        if (From != other.From || Path.Count != other.Path.Count || Captures.Count != other.Captures.Count)
        {
            return false;
        }

        for (int i = 0; i < Path.Count; i++)
        {
            if (Path[i] != other.Path[i])
            {
                return false;
            }
        }

        for (int i = 0; i < Captures.Count; i++)
        {
            if (Captures[i] != other.Captures[i])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as Move);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(From);
        foreach (int step in Path)
        {
            hash.Add(step);
        }

        foreach (int capture in Captures)
        {
            hash.Add(capture);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => ToNotation();
}
