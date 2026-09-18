namespace Dames.Core;

/// <summary>Clés de hachage Zobrist, utilisées pour la table de transposition et la nulle par répétition.</summary>
internal static class Zobrist
{
    private const int PieceKinds = 5; // Empty + 4 types de pièces

    private static readonly ulong[,] PieceKeys = new ulong[Squares.CellCount, PieceKinds];

    internal static readonly ulong BlackToMove;

    static Zobrist()
    {
        var rng = new Random(20260918);
        Span<byte> buffer = stackalloc byte[8];

        for (int cell = 0; cell < Squares.CellCount; cell++)
        {
            for (int kind = 1; kind < PieceKinds; kind++)
            {
                rng.NextBytes(buffer);
                PieceKeys[cell, kind] = BitConverter.ToUInt64(buffer);
            }
        }

        rng.NextBytes(buffer);
        BlackToMove = BitConverter.ToUInt64(buffer);
    }

    internal static ulong Key(int cell, Square piece) =>
        piece == Square.Empty ? 0UL : PieceKeys[cell, (int)piece];

    internal static ulong Compute(Board board, Player sideToMove)
    {
        ulong hash = 0;
        for (int n = 1; n <= Squares.PlayableCount; n++)
        {
            int index = Squares.IndexOf(n);
            hash ^= Key(index, board[index]);
        }

        if (sideToMove == Player.Black)
        {
            hash ^= BlackToMove;
        }

        return hash;
    }
}
