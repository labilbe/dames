namespace Dames.Core;

/// <summary>
/// L'état complet d'une partie : le damier, le trait, l'historique des coups et
/// le suivi des conditions de nulle. Optimisé pour un usage en recherche via
/// <see cref="MakeMove"/> / <see cref="UnmakeMove"/>.
/// </summary>
public sealed class GameState
{
    /// <summary>Nombre de demi-coups sans prise ni mouvement de pion au-delà duquel la partie est nulle.</summary>
    public const int PliesWithoutProgressForDraw = 50;

    /// <summary>Nombre d'occurrences d'une même position entraînant la nulle par répétition.</summary>
    public const int RepetitionsForDraw = 3;

    private readonly Stack<UndoInfo> _undo = new();
    private readonly Dictionary<ulong, int> _repetitions = [];
    private readonly List<Move> _played = [];
    private readonly Board _startingBoard;
    private ulong _hash;

    public GameState()
        : this(Board.CreateInitial(), Player.White)
    {
    }

    public GameState(Board board, Player sideToMove)
    {
        Board = board;
        SideToMove = sideToMove;
        StartingSide = sideToMove;
        _startingBoard = board.Clone();
        _hash = Zobrist.Compute(board, sideToMove);
        _repetitions[_hash] = 1;
    }

    public Board Board { get; }

    /// <summary>Le damier tel qu'il était au premier coup de la partie.</summary>
    public Board StartingBoard => _startingBoard.Clone();

    /// <summary>Le camp qui avait le trait au début de la partie.</summary>
    public Player StartingSide { get; }

    /// <summary>Les coups joués depuis le début de la partie, dans l'ordre.</summary>
    public IReadOnlyList<Move> History => _played;

    public Player SideToMove { get; private set; }

    public int PliesWithoutProgress { get; private set; }

    public ulong Hash => _hash;

    /// <summary>Nombre de demi-coups joués depuis le début de la partie.</summary>
    public int PlyCount => _undo.Count;

    public List<Move> LegalMoves() => MoveGenerator.Generate(Board, SideToMove);

    public GameState Clone()
    {
        var copy = new GameState(Board.Clone(), SideToMove)
        {
            PliesWithoutProgress = PliesWithoutProgress,
        };
        return copy;
    }

    /// <summary>Joue un coup et met à jour le trait, le hachage et les compteurs de nulle.</summary>
    public void MakeMove(Move move)
    {
        Square moved = Board[move.From];
        var capturedPieces = new (int Index, Square Piece)[move.Captures.Count];

        _hash ^= Zobrist.Key(move.From, moved);
        Board[move.From] = Square.Empty;

        for (int i = 0; i < move.Captures.Count; i++)
        {
            int index = move.Captures[i];
            Square piece = Board[index];
            capturedPieces[i] = (index, piece);
            _hash ^= Zobrist.Key(index, piece);
            Board[index] = Square.Empty;
        }

        Square landed = move.Promotes ? SideToMove.King() : moved;
        Board[move.To] = landed;
        _hash ^= Zobrist.Key(move.To, landed);
        _hash ^= Zobrist.BlackToMove;

        var undo = new UndoInfo(move, moved, capturedPieces, PliesWithoutProgress, _hash);
        _undo.Push(undo);
        _played.Add(move);

        PliesWithoutProgress = move.IsCapture || moved.IsMan() ? 0 : PliesWithoutProgress + 1;
        SideToMove = SideToMove.Opponent();

        // Une prise ou un coup de pion est irréversible : l'historique des répétitions repart de zéro.
        if (PliesWithoutProgress == 0)
        {
            _repetitions.Clear();
        }

        _repetitions[_hash] = _repetitions.GetValueOrDefault(_hash) + 1;
    }

    /// <summary>Annule le dernier coup joué.</summary>
    public void UnmakeMove()
    {
        UndoInfo undo = _undo.Pop();
        Move move = undo.Move;
        _played.RemoveAt(_played.Count - 1);

        int count = _repetitions.GetValueOrDefault(_hash);
        if (count <= 1)
        {
            _repetitions.Remove(_hash);
        }
        else
        {
            _repetitions[_hash] = count - 1;
        }

        SideToMove = SideToMove.Opponent();
        PliesWithoutProgress = undo.PreviousPliesWithoutProgress;

        _hash ^= Zobrist.Key(move.To, Board[move.To]);
        Board[move.To] = Square.Empty;

        foreach ((int index, Square piece) in undo.Captured)
        {
            Board[index] = piece;
            _hash ^= Zobrist.Key(index, piece);
        }

        Board[move.From] = undo.MovedPiece;
        _hash ^= Zobrist.Key(move.From, undo.MovedPiece);
        _hash ^= Zobrist.BlackToMove;
    }

    /// <summary>Le résultat de la partie dans la position courante.</summary>
    public GameResult Result()
    {
        if (LegalMoves().Count == 0)
        {
            // Sans coup légal — plus de pièces ou totalement bloqué — le camp au trait perd.
            return SideToMove == Player.White ? GameResult.BlackWins : GameResult.WhiteWins;
        }

        if (PliesWithoutProgress >= PliesWithoutProgressForDraw)
        {
            return GameResult.Draw;
        }

        if (_repetitions.GetValueOrDefault(_hash) >= RepetitionsForDraw)
        {
            return GameResult.Draw;
        }

        return GameResult.InProgress;
    }

    /// <summary>Vrai si la position courante est déjà apparue le nombre de fois requis pour la nulle.</summary>
    public bool IsRepetitionDraw() => _repetitions.GetValueOrDefault(_hash) >= RepetitionsForDraw;

    private readonly record struct UndoInfo(
        Move Move,
        Square MovedPiece,
        (int Index, Square Piece)[] Captured,
        int PreviousPliesWithoutProgress,
        ulong HashAfter);
}
