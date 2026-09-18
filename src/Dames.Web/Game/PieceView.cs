using Dames.Core;

namespace Dames.Web.Game;

/// <summary>Qui tient un camp.</summary>
public enum Seat
{
    Human,
    Computer,
}

/// <summary>
/// Une pièce telle qu'elle est affichée. Son identité survit aux déplacements, ce qui permet
/// d'animer la pièce plutôt que de la redessiner à chaque coup.
/// </summary>
public sealed class PieceView
{
    public required int Id { get; init; }

    public required Player Owner { get; init; }

    public bool IsKing { get; set; }

    public int Row { get; set; }

    public int Col { get; set; }

    /// <summary>Vrai dès que la pièce est prise : elle s'efface puis disparaît.</summary>
    public bool Captured { get; set; }
}

/// <summary>Une entrée du relevé de la partie.</summary>
public sealed record MoveRecord(Player Player, string Notation, int Captures, bool Promotes);
