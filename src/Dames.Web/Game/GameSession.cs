using Dames.Core;
using Dames.Core.Pdn;

namespace Dames.Web.Game;

/// <summary>
/// Le pilote de la partie côté interface : il tient l'état du jeu, la sélection en cours,
/// le relevé des coups et l'animation des pièces. Toute la logique de règles vient de Dames.Core.
/// </summary>
public sealed class GameSession
{
    private const int HopMilliseconds = 260;
    private const int FadeMilliseconds = 240;
    private const int ThinkingPaintDelay = 60;

    private readonly List<PieceView> _pieces = [];
    private readonly Dictionary<int, PieceView> _pieceBySquare = [];
    private readonly List<MoveRecord> _history = [];
    private readonly List<int> _path = [];

    private GameState _state = new();
    private SearchEngine _engine;
    private List<Move> _legal = [];
    private List<Move> _candidates = [];
    private int _nextPieceId;

    /// <summary>Vrai tant qu'une boucle de jeu de l'ordinateur tourne : il ne doit y en avoir qu'une.</summary>
    private bool _computerBusy;

    /// <summary>Incrémenté à chaque remise à zéro, pour qu'une boucle en cours abandonne la partie abandonnée.</summary>
    private int _generation;

    public GameSession()
    {
        _engine = CreateEngine(Difficulty.Medium);
        Reset();
    }

    /// <summary>Signale à l'interface qu'il faut redessiner.</summary>
    public event Action? Changed;

    public IReadOnlyList<PieceView> Pieces => _pieces;

    public IReadOnlyList<MoveRecord> History => _history;

    public Player SideToMove => _state.SideToMove;

    public GameResult Result { get; private set; } = GameResult.InProgress;

    public Seat WhiteSeat { get; private set; } = Seat.Human;

    public Seat BlackSeat { get; private set; } = Seat.Computer;

    public Difficulty Difficulty { get; private set; } = Difficulty.Medium;

    public bool ShowNumbers { get; set; } = true;

    /// <summary>
    /// Vrai quand le damier doit être retourné : le joueur humain voit toujours son camp
    /// en bas, comme devant une vraie table.
    /// </summary>
    public bool Flipped => BlackSeat == Seat.Human && WhiteSeat == Seat.Computer;

    /// <summary>Vrai si la partie a commencé par un coup des Noirs, ce qui décale le relevé.</summary>
    public bool OpensWithBlack => _state.StartingSide == Player.Black;

    /// <summary>Ligne d'affichage d'une ligne du damier, orientation comprise.</summary>
    public int ViewRow(int row) => Flipped ? Squares.Size - 1 - row : row;

    /// <summary>Colonne d'affichage d'une colonne du damier, orientation comprise.</summary>
    public int ViewCol(int col) => Flipped ? Squares.Size - 1 - col : col;

    public bool IsThinking { get; private set; }

    public bool IsAnimating { get; private set; }

    public bool Busy => IsThinking || IsAnimating;

    /// <summary>Case de départ choisie, le cas échéant.</summary>
    public int? Origin { get; private set; }

    /// <summary>Cases déjà validées d'une rafle en cours de saisie.</summary>
    public IReadOnlyList<int> Path => _path;

    /// <summary>Cases où la pièce sélectionnée peut se poser au prochain bond.</summary>
    public IReadOnlySet<int> Landings { get; private set; } = new HashSet<int>();

    /// <summary>Pièces que la sélection courante va emporter.</summary>
    public IReadOnlySet<int> Doomed { get; private set; } = new HashSet<int>();

    /// <summary>Départ et arrivée du dernier coup joué, pour le signaler sur le damier.</summary>
    public (int From, int To)? LastMove { get; private set; }

    public int WhiteCount => _state.Board.CountOf(Player.White);

    public int BlackCount => _state.Board.CountOf(Player.Black);

    public bool CanUndo => _state.PlyCount > 0 && !Busy;

    /// <summary>Nombre de pièces que le camp au trait est obligé de prendre, 0 s'il n'y a pas de prise.</summary>
    public int ForcedCaptureCount => _legal.Count > 0 && _legal[0].IsCapture ? _legal[0].CaptureCount : 0;

    public Seat SeatOf(Player player) => player == Player.White ? WhiteSeat : BlackSeat;

    public bool IsHumanTurn => !Result.IsOver() && SeatOf(SideToMove) == Seat.Human;

    public async Task NewGameAsync()
    {
        _generation++;
        Reset();
        Changed?.Invoke();
        await RunComputerAsync();
    }

    /// <summary>La partie en cours au format PDN, prête à être copiée ou enregistrée.</summary>
    public string ToPdn() => PdnFile.Write(_state, [
        new PdnTag("Event", "Partie amicale"),
        new PdnTag("Site", "Dames, dans le navigateur"),
        new PdnTag("White", SeatName(Player.White)),
        new PdnTag("Black", SeatName(Player.Black)),
    ]);

    /// <summary>
    /// Remplace la partie en cours par celle décrite en PDN. En cas de texte invalide,
    /// la partie en cours n'est pas touchée et le message d'erreur explique où ça coince.
    /// </summary>
    public async Task<string?> ImportPdnAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Collez d'abord un PDN.";
        }

        PdnGame game;
        try
        {
            game = PdnFile.Parse(text);
        }
        catch (PdnException error)
        {
            return error.Message;
        }
        catch (ArgumentException error)
        {
            return error.Message;
        }

        _generation++;
        Load(game.ToGameState());
        Changed?.Invoke();
        await RunComputerAsync();
        return null;
    }

    private string SeatName(Player player) =>
        SeatOf(player) == Seat.Human ? "Humain" : $"Ordinateur ({SearchOptions.ToFrench(Difficulty)})";

    public async Task SetSeatAsync(Player player, Seat seat)
    {
        if (player == Player.White)
        {
            WhiteSeat = seat;
        }
        else
        {
            BlackSeat = seat;
        }

        ClearSelection();
        Changed?.Invoke();
        await RunComputerAsync();
    }

    public async Task SetDifficultyAsync(Difficulty difficulty)
    {
        Difficulty = difficulty;
        _engine = CreateEngine(difficulty);
        Changed?.Invoke();
        await Task.CompletedTask;
    }

    /// <summary>Annule le dernier coup humain, et la réponse de l'ordinateur s'il y en a une.</summary>
    public void Undo()
    {
        if (Busy || _state.PlyCount == 0)
        {
            return;
        }

        _generation++;
        int steps = WhiteSeat == BlackSeat ? 1 : 2;
        for (int i = 0; i < steps && _state.PlyCount > 0; i++)
        {
            _state.UnmakeMove();
            _history.RemoveAt(_history.Count - 1);
        }

        LastMove = null;
        RebuildPieces();
        ClearSelection();
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>Traite un clic sur une case du damier.</summary>
    public async Task ClickAsync(int square)
    {
        if (Busy || !IsHumanTurn)
        {
            return;
        }

        // Cliquer une de ses pièces (re)commence la saisie d'un coup.
        if (_state.Board[square].BelongsTo(SideToMove))
        {
            List<Move> fromHere = [.. _legal.Where(m => m.From == square)];
            if (fromHere.Count > 0)
            {
                Origin = square;
                _path.Clear();
                _candidates = fromHere;
                UpdateHints();
                Changed?.Invoke();
            }

            return;
        }

        if (Origin is null)
        {
            return;
        }

        List<Move> remaining = [.. _candidates.Where(m => m.Path.Count > _path.Count && m.Path[_path.Count] == square)];
        if (remaining.Count == 0)
        {
            ClearSelection();
            Changed?.Invoke();
            return;
        }

        _path.Add(square);
        _candidates = remaining;

        // Toutes les rafles d'une même pièce prennent le même nombre de pièces :
        // le coup est complet dès que le chemin saisi atteint la longueur attendue.
        if (_candidates[0].Path.Count == _path.Count)
        {
            Move move = _candidates[0];
            ClearSelection();
            await PlayAsync(move);
            await RunComputerAsync();
            return;
        }

        UpdateHints();
        Changed?.Invoke();
    }

    private static SearchEngine CreateEngine(Difficulty difficulty)
    {
        // Le navigateur n'a qu'un fil d'exécution : on raccourcit la réflexion
        // pour que l'interface ne reste jamais figée plus de deux secondes.
        TimeSpan limit = difficulty switch
        {
            Difficulty.Easy => TimeSpan.FromMilliseconds(120),
            Difficulty.Medium => TimeSpan.FromMilliseconds(500),
            Difficulty.Hard => TimeSpan.FromMilliseconds(1200),
            _ => TimeSpan.FromMilliseconds(2500),
        };

        return new SearchEngine(SearchOptions.For(difficulty) with { TimeLimit = limit });
    }

    private void Reset() => Load(new GameState());

    /// <summary>Prend une partie comme état courant et reconstruit tout ce que l'affichage en tire.</summary>
    private void Load(GameState state)
    {
        _state = state;

        _history.Clear();
        foreach (Move move in state.History)
        {
            _history.Add(new MoveRecord(PlayerOfPly(state, _history.Count), move.ToNotation(), move.CaptureCount, move.Promotes));
        }

        LastMove = state.History.Count > 0 ? (state.History[^1].From, state.History[^1].To) : null;
        RebuildPieces();
        ClearSelection();
        Refresh();
    }

    private static Player PlayerOfPly(GameState state, int ply) =>
        ply % 2 == 0 ? state.StartingSide : state.StartingSide.Opponent();

    private void RebuildPieces()
    {
        _pieces.Clear();
        _pieceBySquare.Clear();
        _nextPieceId = 0;

        for (int number = 1; number <= Squares.PlayableCount; number++)
        {
            int index = Squares.IndexOf(number);
            Square square = _state.Board[index];
            if (square == Square.Empty)
            {
                continue;
            }

            var piece = new PieceView
            {
                Id = _nextPieceId++,
                Owner = square.Owner(),
                IsKing = square.IsKing(),
                Row = Squares.Row(index),
                Col = Squares.Col(index),
            };

            _pieces.Add(piece);
            _pieceBySquare[index] = piece;
        }
    }

    private void ClearSelection()
    {
        Origin = null;
        _path.Clear();
        _candidates = [];
        Landings = new HashSet<int>();
        Doomed = new HashSet<int>();
    }

    private void Refresh()
    {
        _legal = _state.LegalMoves();
        Result = _state.Result();
    }

    private void UpdateHints()
    {
        Landings = _candidates
            .Where(m => m.Path.Count > _path.Count)
            .Select(m => m.Path[_path.Count])
            .ToHashSet();

        // Une seule rafle possible : on montre tout ce qu'elle emporte.
        // Plusieurs : on ne montre que la pièce du prochain bond, la suite n'est pas encore décidée.
        Doomed = _candidates.Count == 1
            ? _candidates[0].Captures.Skip(_path.Count).ToHashSet()
            : _candidates.Where(m => m.Captures.Count > _path.Count)
                .Select(m => m.Captures[_path.Count])
                .ToHashSet();
    }

    private async Task PlayAsync(Move move)
    {
        _history.Add(new MoveRecord(SideToMove, move.ToNotation(), move.CaptureCount, move.Promotes));
        LastMove = (move.From, move.To);

        _state.MakeMove(move);
        Refresh();

        await AnimateAsync(move);
    }

    private async Task AnimateAsync(Move move)
    {
        IsAnimating = true;

        if (!_pieceBySquare.Remove(move.From, out PieceView? mover))
        {
            IsAnimating = false;
            return;
        }

        var fading = new List<PieceView>();

        for (int step = 0; step < move.Path.Count; step++)
        {
            if (step < move.Captures.Count
                && _pieceBySquare.Remove(move.Captures[step], out PieceView? victim))
            {
                victim.Captured = true;
                fading.Add(victim);
            }

            int landing = move.Path[step];
            mover.Row = Squares.Row(landing);
            mover.Col = Squares.Col(landing);

            Changed?.Invoke();
            await Task.Delay(HopMilliseconds);
        }

        if (move.Promotes)
        {
            mover.IsKing = true;
        }

        _pieceBySquare[move.To] = mover;
        Changed?.Invoke();

        if (fading.Count > 0)
        {
            await Task.Delay(FadeMilliseconds);
            _pieces.RemoveAll(fading.Contains);
        }

        IsAnimating = false;
        Changed?.Invoke();
    }

    /// <summary>
    /// Fait jouer l'ordinateur tant que c'est à lui. Une seule boucle tourne à la fois : changer
    /// de camp en cours de partie ne doit pas en démarrer une seconde, la boucle en place voit le
    /// changement d'elle-même. Une remise à zéro la fait abandonner, puis en relance une propre.
    /// </summary>
    private async Task RunComputerAsync()
    {
        if (_computerBusy)
        {
            return;
        }

        _computerBusy = true;
        int generation = _generation;

        try
        {
            while (generation == _generation && !Result.IsOver() && SeatOf(SideToMove) == Seat.Computer)
            {
                IsThinking = true;
                Changed?.Invoke();

                // Laisse le navigateur peindre l'état « réfléchit » avant de bloquer le fil sur la recherche.
                await Task.Delay(ThinkingPaintDelay);

                SearchResult result = _engine.Search(_state);
                IsThinking = false;

                if (generation != _generation || result.BestMove is null)
                {
                    Changed?.Invoke();
                    break;
                }

                await PlayAsync(result.BestMove);
            }
        }
        finally
        {
            _computerBusy = false;
        }

        if (generation != _generation)
        {
            await RunComputerAsync();
        }
    }
}
