using System.Text;

namespace Dames.Core.Pdn;

/// <summary>
/// Le champ FEN du format PDN, qui décrit une position complète :
/// <c>W:W31-50:B1-20</c> — le trait, puis les pièces de chaque camp,
/// les dames étant préfixées d'un K.
/// </summary>
public static class PdnFen
{
    /// <summary>Écrit la position sous forme de FEN PDN.</summary>
    public static string Write(Board board, Player sideToMove)
    {
        var sb = new StringBuilder();
        sb.Append(sideToMove == Player.White ? 'W' : 'B');
        AppendSide(sb, board, Player.White);
        AppendSide(sb, board, Player.Black);
        return sb.ToString();
    }

    /// <summary>Lit un FEN PDN. Les deux camps peuvent être donnés dans n'importe quel ordre.</summary>
    public static (Board Board, Player SideToMove) Parse(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);

        string[] fields = fen.Trim().TrimEnd('.').Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 0)
        {
            throw new PdnException("FEN vide.");
        }

        Player sideToMove = fields[0].Trim().ToUpperInvariant() switch
        {
            "W" => Player.White,
            "B" => Player.Black,
            var other => throw new PdnException($"Trait inconnu dans le FEN : '{other}'. Attendu W ou B."),
        };

        var board = new Board();
        for (int i = 1; i < fields.Length; i++)
        {
            string field = fields[i].Trim();
            if (field.Length == 0)
            {
                continue;
            }

            Player owner = char.ToUpperInvariant(field[0]) switch
            {
                'W' => Player.White,
                'B' => Player.Black,
                _ => throw new PdnException($"Camp inconnu dans le FEN : '{field}'."),
            };

            ReadSquares(board, owner, field[1..]);
        }

        return (board, sideToMove);
    }

    private static void AppendSide(StringBuilder sb, Board board, Player player)
    {
        sb.Append(':').Append(player == Player.White ? 'W' : 'B');

        bool first = true;
        for (int number = 1; number <= Squares.PlayableCount; number++)
        {
            Square piece = board.AtNumber(number);
            if (!piece.BelongsTo(player))
            {
                continue;
            }

            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            if (piece.IsKing())
            {
                sb.Append('K');
            }

            sb.Append(number);
        }
    }

    private static void ReadSquares(Board board, Player owner, string list)
    {
        foreach (string raw in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = raw.Trim();
            if (token.Length == 0)
            {
                continue;
            }

            bool king = char.ToUpperInvariant(token[0]) == 'K';
            if (king)
            {
                token = token[1..];
            }

            Square piece = king ? owner.King() : owner.Man();

            // Les intervalles ne sont pas produits par l'écriture mais restent acceptés en lecture.
            int dash = token.IndexOf('-');
            if (dash > 0)
            {
                int from = ReadNumber(token[..dash]);
                int to = ReadNumber(token[(dash + 1)..]);
                if (from > to)
                {
                    throw new PdnException($"Intervalle inversé dans le FEN : '{token}'.");
                }

                for (int n = from; n <= to; n++)
                {
                    board.SetNumber(n, piece);
                }
            }
            else
            {
                board.SetNumber(ReadNumber(token), piece);
            }
        }
    }

    private static int ReadNumber(string token)
    {
        if (!int.TryParse(token, out int number) || number is < 1 or > Squares.PlayableCount)
        {
            throw new PdnException($"Case invalide dans le FEN : '{token}'. Attendu 1 à {Squares.PlayableCount}.");
        }

        return number;
    }
}
