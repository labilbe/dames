using System.Text;

namespace Dames.Core;

/// <summary>Un damier 10x10 mutable. Seules les 50 cases sombres sont utilisées.</summary>
public sealed class Board
{
    private const string ColumnHeader = "     a   b   c   d   e   f   g   h   i   j";
    private const string Separator = "   +---+---+---+---+---+---+---+---+---+---+";

    private readonly Square[] _cells;

    private Board(Square[] cells) => _cells = cells;

    public Board() => _cells = new Square[Squares.CellCount];

    public Square this[int index]
    {
        get => _cells[index];
        set => _cells[index] = value;
    }

    public Square this[int row, int col]
    {
        get => _cells[Squares.Index(row, col)];
        set => _cells[Squares.Index(row, col)] = value;
    }

    /// <summary>Accès par numéro officiel de case (1 à 50).</summary>
    public Square AtNumber(int number) => _cells[Squares.IndexOf(number)];

    public void SetNumber(int number, Square value) => _cells[Squares.IndexOf(number)] = value;

    /// <summary>Position de départ : Noirs sur les cases 1 à 20, Blancs sur les cases 31 à 50.</summary>
    public static Board CreateInitial()
    {
        var board = new Board();
        for (int n = 1; n <= 20; n++)
        {
            board.SetNumber(n, Square.BlackMan);
        }

        for (int n = 31; n <= 50; n++)
        {
            board.SetNumber(n, Square.WhiteMan);
        }

        return board;
    }

    public Board Clone() => new((Square[])_cells.Clone());

    /// <summary>Énumère les index internes des cases occupées par le camp donné.</summary>
    public IEnumerable<int> PiecesOf(Player player)
    {
        for (int n = 1; n <= Squares.PlayableCount; n++)
        {
            int index = Squares.IndexOf(n);
            if (_cells[index].BelongsTo(player))
            {
                yield return index;
            }
        }
    }

    public int Count(Square kind)
    {
        int total = 0;
        for (int n = 1; n <= Squares.PlayableCount; n++)
        {
            if (_cells[Squares.IndexOf(n)] == kind)
            {
                total++;
            }
        }

        return total;
    }

    public int CountOf(Player player) => PiecesOf(player).Count();

    /// <summary>
    /// Construit un damier à partir d'une description compacte : "W:31-50 B:1-20",
    /// les dames étant préfixées par K (par exemple "W:K45,31 B:K8").
    /// </summary>
    public static Board FromPositionString(string description)
    {
        var board = new Board();
        foreach (string chunk in description.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = chunk.Split(':', 2);
            if (parts.Length != 2)
            {
                throw new FormatException($"Segment invalide : '{chunk}'.");
            }

            Player player = parts[0].ToUpperInvariant() switch
            {
                "W" => Player.White,
                "B" => Player.Black,
                _ => throw new FormatException($"Camp inconnu : '{parts[0]}'."),
            };

            foreach (string token in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string item = token.Trim();
                bool king = item.StartsWith('K') || item.StartsWith('k');
                if (king)
                {
                    item = item[1..];
                }

                Square piece = king ? player.King() : player.Man();
                int dash = item.IndexOf('-');
                if (dash > 0)
                {
                    int from = int.Parse(item[..dash]);
                    int to = int.Parse(item[(dash + 1)..]);
                    for (int n = from; n <= to; n++)
                    {
                        board.SetNumber(n, piece);
                    }
                }
                else
                {
                    board.SetNumber(int.Parse(item), piece);
                }
            }
        }

        return board;
    }

    /// <summary>Représentation texte du damier, vue côté Blancs.</summary>
    public string Render(bool showNumbers = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ColumnHeader);
        sb.AppendLine(Separator);
        for (int row = 0; row < Squares.Size; row++)
        {
            sb.Append((Squares.Size - row).ToString().PadLeft(2)).Append(' ').Append('|');
            for (int col = 0; col < Squares.Size; col++)
            {
                if (!Squares.IsPlayable(row, col))
                {
                    sb.Append("   |");
                    continue;
                }

                Square piece = this[row, col];
                if (piece == Square.Empty)
                {
                    int number = Squares.NumberOf(Squares.Index(row, col));
                    sb.Append(showNumbers ? $"{number,3}" : " . ").Append('|');
                }
                else
                {
                    char glyph = piece switch
                    {
                        Square.WhiteMan => 'o',
                        Square.WhiteKing => 'O',
                        Square.BlackMan => 'x',
                        Square.BlackKing => 'X',
                        _ => ' ',
                    };
                    sb.Append(' ').Append(glyph).Append(' ').Append('|');
                }
            }

            sb.Append(' ').Append(Squares.Size - row).AppendLine();
            sb.AppendLine(Separator);
        }

        sb.AppendLine(ColumnHeader);
        return sb.ToString();
    }

    public override string ToString() => Render();
}
