namespace Dames.Core;

/// <summary>Évaluation statique d'une position, exprimée en centipions du point de vue des Blancs.</summary>
public static class Evaluator
{
    public const int ManValue = 100;
    public const int KingValue = 320;

    /// <summary>Score attribué à un gain ; on en retranche la profondeur pour préférer les gains rapides.</summary>
    public const int WinScore = 1_000_000;

    private const int AdvancementBonus = 3;
    private const int EdgeManBonus = 4;
    private const int BackRowBonus = 6;
    private const int KingCenterBonus = 4;

    /// <summary>Score de la position, positif si les Blancs sont mieux.</summary>
    public static int Evaluate(Board board)
    {
        int score = 0;
        int whitePieces = 0;
        int blackPieces = 0;

        for (int n = 1; n <= Squares.PlayableCount; n++)
        {
            int index = Squares.IndexOf(n);
            Square piece = board[index];
            if (piece == Square.Empty)
            {
                continue;
            }

            Player owner = piece.Owner();
            int row = Squares.Row(index);
            int col = Squares.Col(index);
            int value;

            if (piece.IsKing())
            {
                value = KingValue + (KingCenterBonus * Centrality(row, col));
            }
            else
            {
                int advanced = owner == Player.White ? Squares.Size - 1 - row : row;
                value = ManValue + (AdvancementBonus * advanced);

                if (col is 0 or Squares.Size - 1)
                {
                    value += EdgeManBonus;
                }

                if (row == Squares.PromotionRow(owner.Opponent()))
                {
                    // Le pion est resté sur sa rangée de fond : il verrouille la promotion adverse.
                    value += BackRowBonus;
                }
            }

            if (owner == Player.White)
            {
                score += value;
                whitePieces++;
            }
            else
            {
                score -= value;
                blackPieces++;
            }
        }

        // En finale, le camp qui mène gagne à simplifier : on accentue légèrement l'écart.
        int total = whitePieces + blackPieces;
        if (total <= 8 && total > 0)
        {
            score += score * (8 - total) / 16;
        }

        return score;
    }

    /// <summary>Score du point de vue du camp donné.</summary>
    public static int Evaluate(Board board, Player player)
    {
        int score = Evaluate(board);
        return player == Player.White ? score : -score;
    }

    /// <summary>0 sur le bord, 4 au centre du damier.</summary>
    private static int Centrality(int row, int col)
    {
        int dr = Math.Min(row, Squares.Size - 1 - row);
        int dc = Math.Min(col, Squares.Size - 1 - col);
        return Math.Min(dr, dc);
    }
}
