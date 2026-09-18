namespace Dames.Core;

/// <summary>Les deux camps. Les Blancs occupent le bas du damier et avancent vers la rangée 0.</summary>
public enum Player
{
    White,
    Black,
}

/// <summary>Contenu d'une case du damier.</summary>
public enum Square : byte
{
    Empty = 0,
    WhiteMan,
    WhiteKing,
    BlackMan,
    BlackKing,
}

public static class SquareExtensions
{
    public static bool IsEmpty(this Square s) => s == Square.Empty;

    public static bool IsKing(this Square s) => s is Square.WhiteKing or Square.BlackKing;

    public static bool IsMan(this Square s) => s is Square.WhiteMan or Square.BlackMan;

    public static Player Owner(this Square s) =>
        s is Square.WhiteMan or Square.WhiteKing ? Player.White : Player.Black;

    public static bool BelongsTo(this Square s, Player p) =>
        s != Square.Empty && s.Owner() == p;

    public static Player Opponent(this Player p) =>
        p == Player.White ? Player.Black : Player.White;

    public static Square Man(this Player p) => p == Player.White ? Square.WhiteMan : Square.BlackMan;

    public static Square King(this Player p) => p == Player.White ? Square.WhiteKing : Square.BlackKing;
}

/// <summary>
/// Conversions entre la numérotation officielle des dames internationales (1 à 50,
/// cases sombres numérotées de gauche à droite puis de haut en bas) et les
/// coordonnées internes ligne/colonne d'un damier 10x10.
/// </summary>
public static class Squares
{
    public const int Size = 10;
    public const int CellCount = Size * Size;
    public const int PlayableCount = 50;

    /// <summary>Les quatre directions diagonales, sous forme (deltaLigne, deltaColonne).</summary>
    public static readonly (int Dr, int Dc)[] Directions =
    [
        (-1, -1), (-1, 1), (1, -1), (1, 1),
    ];

    private static readonly int[] IndexByNumber = new int[PlayableCount + 1];
    private static readonly int[] NumberByIndex = new int[CellCount];

    static Squares()
    {
        Array.Fill(NumberByIndex, 0);
        for (int number = 1; number <= PlayableCount; number++)
        {
            int row = (number - 1) / 5;
            int k = (number - 1) % 5;
            int col = row % 2 == 0 ? (k * 2) + 1 : k * 2;
            int index = (row * Size) + col;
            IndexByNumber[number] = index;
            NumberByIndex[index] = number;
        }
    }

    public static bool IsPlayable(int row, int col) =>
        row >= 0 && row < Size && col >= 0 && col < Size && ((row + col) & 1) == 1;

    public static bool IsOnBoard(int row, int col) =>
        row >= 0 && row < Size && col >= 0 && col < Size;

    public static int Index(int row, int col) => (row * Size) + col;

    public static int Row(int index) => index / Size;

    public static int Col(int index) => index % Size;

    /// <summary>Index interne (0 à 99) de la case portant le numéro officiel donné (1 à 50).</summary>
    public static int IndexOf(int number)
    {
        if (number is < 1 or > PlayableCount)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Le numéro de case doit être compris entre 1 et 50.");
        }

        return IndexByNumber[number];
    }

    /// <summary>Numéro officiel (1 à 50) de la case d'index interne donné, ou 0 si la case est claire.</summary>
    public static int NumberOf(int index) => NumberByIndex[index];

    /// <summary>Rangée de promotion du camp donné.</summary>
    public static int PromotionRow(Player player) => player == Player.White ? 0 : Size - 1;

    /// <summary>Sens d'avance des pions du camp donné (-1 pour les Blancs, +1 pour les Noirs).</summary>
    public static int Forward(Player player) => player == Player.White ? -1 : 1;
}
