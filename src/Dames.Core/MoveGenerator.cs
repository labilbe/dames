namespace Dames.Core;

/// <summary>
/// Génération des coups légaux selon les règles des dames internationales (FMJD) :
/// prise dans les quatre directions, dame volante, prise obligatoire et
/// obligation de prendre le maximum de pièces.
/// </summary>
public static class MoveGenerator
{
    /// <summary>Tous les coups légaux du camp donné, rafle maximale déjà appliquée.</summary>
    public static List<Move> Generate(Board board, Player player)
    {
        List<Move> captures = GenerateCaptures(board, player);
        return captures.Count > 0 ? captures : GenerateQuietMoves(board, player);
    }

    public static bool HasAnyMove(Board board, Player player) => Generate(board, player).Count > 0;

    /// <summary>Rafles légales, filtrées par la règle du maximum. Liste vide s'il n'y a aucune prise.</summary>
    public static List<Move> GenerateCaptures(Board board, Player player)
    {
        var results = new List<Move>();
        var work = board.Clone();
        var captured = new bool[Squares.CellCount];
        var path = new List<int>();
        var caps = new List<int>();

        foreach (int origin in board.PiecesOf(player).ToList())
        {
            Square piece = board[origin];
            work[origin] = Square.Empty;

            if (piece.IsKing())
            {
                ExploreKingCaptures(work, captured, player, origin, origin, path, caps, results);
            }
            else
            {
                ExploreManCaptures(work, captured, player, origin, origin, path, caps, results);
            }

            work[origin] = piece;
        }

        return KeepMaximalCaptures(results);
    }

    /// <summary>Déplacements simples (sans prise) du camp donné.</summary>
    public static List<Move> GenerateQuietMoves(Board board, Player player)
    {
        var results = new List<Move>();
        int promotionRow = Squares.PromotionRow(player);

        foreach (int origin in board.PiecesOf(player))
        {
            Square piece = board[origin];
            int row = Squares.Row(origin);
            int col = Squares.Col(origin);

            if (piece.IsKing())
            {
                foreach ((int dr, int dc) in Squares.Directions)
                {
                    int r = row + dr;
                    int c = col + dc;
                    while (Squares.IsOnBoard(r, c) && board[r, c] == Square.Empty)
                    {
                        results.Add(new Move(origin, [Squares.Index(r, c)], [], promotes: false));
                        r += dr;
                        c += dc;
                    }
                }
            }
            else
            {
                int forward = Squares.Forward(player);
                foreach (int dc in (int[])[-1, 1])
                {
                    int r = row + forward;
                    int c = col + dc;
                    if (Squares.IsOnBoard(r, c) && board[r, c] == Square.Empty)
                    {
                        results.Add(new Move(origin, [Squares.Index(r, c)], [], promotes: r == promotionRow));
                    }
                }
            }
        }

        return results;
    }

    private static void ExploreManCaptures(
        Board work,
        bool[] captured,
        Player player,
        int origin,
        int current,
        List<int> path,
        List<int> caps,
        List<Move> results)
    {
        int row = Squares.Row(current);
        int col = Squares.Col(current);
        bool extended = false;

        foreach ((int dr, int dc) in Squares.Directions)
        {
            int vr = row + dr;
            int vc = col + dc;
            int lr = row + (2 * dr);
            int lc = col + (2 * dc);
            if (!Squares.IsOnBoard(lr, lc))
            {
                continue;
            }

            int victim = Squares.Index(vr, vc);
            int landing = Squares.Index(lr, lc);
            if (captured[victim] || !work[victim].BelongsTo(player.Opponent()) || work[landing] != Square.Empty)
            {
                continue;
            }

            extended = true;
            captured[victim] = true;
            caps.Add(victim);
            path.Add(landing);

            ExploreManCaptures(work, captured, player, origin, landing, path, caps, results);

            path.RemoveAt(path.Count - 1);
            caps.RemoveAt(caps.Count - 1);
            captured[victim] = false;
        }

        // Un pion qui traverse la rangée de promotion sans s'y arrêter reste pion :
        // la promotion n'est évaluée qu'une fois la rafle terminée.
        if (!extended && caps.Count > 0)
        {
            results.Add(new Move(
                origin,
                [.. path],
                [.. caps],
                promotes: Squares.Row(current) == Squares.PromotionRow(player)));
        }
    }

    private static void ExploreKingCaptures(
        Board work,
        bool[] captured,
        Player player,
        int origin,
        int current,
        List<int> path,
        List<int> caps,
        List<Move> results)
    {
        int row = Squares.Row(current);
        int col = Squares.Col(current);
        bool extended = false;

        foreach ((int dr, int dc) in Squares.Directions)
        {
            int r = row + dr;
            int c = col + dc;
            while (Squares.IsOnBoard(r, c) && work[Squares.Index(r, c)] == Square.Empty)
            {
                r += dr;
                c += dc;
            }

            if (!Squares.IsOnBoard(r, c))
            {
                continue;
            }

            int victim = Squares.Index(r, c);
            if (captured[victim] || !work[victim].BelongsTo(player.Opponent()))
            {
                continue;
            }

            int lr = r + dr;
            int lc = c + dc;
            while (Squares.IsOnBoard(lr, lc) && work[Squares.Index(lr, lc)] == Square.Empty)
            {
                int landing = Squares.Index(lr, lc);
                extended = true;
                captured[victim] = true;
                caps.Add(victim);
                path.Add(landing);

                ExploreKingCaptures(work, captured, player, origin, landing, path, caps, results);

                path.RemoveAt(path.Count - 1);
                caps.RemoveAt(caps.Count - 1);
                captured[victim] = false;

                lr += dr;
                lc += dc;
            }
        }

        if (!extended && caps.Count > 0)
        {
            results.Add(new Move(origin, [.. path], [.. caps], promotes: false));
        }
    }

    /// <summary>
    /// Applique la règle du maximum puis retire les rafles équivalentes : même départ,
    /// même arrivée et même ensemble de pièces prises donnent la même position finale.
    /// </summary>
    private static List<Move> KeepMaximalCaptures(List<Move> moves)
    {
        if (moves.Count == 0)
        {
            return moves;
        }

        int best = moves.Max(m => m.CaptureCount);
        var kept = new List<Move>();
        var seen = new HashSet<string>();

        foreach (Move move in moves)
        {
            if (move.CaptureCount != best)
            {
                continue;
            }

            string signature = $"{move.From}>{move.To}:{string.Join(',', move.Captures.Order())}";
            if (seen.Add(signature))
            {
                kept.Add(move);
            }
        }

        return kept;
    }
}
