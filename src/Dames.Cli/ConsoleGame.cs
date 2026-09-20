using Dames.Core;
using Dames.Core.Pdn;

namespace Dames.Cli;

/// <summary>Déroulement d'une partie dans le terminal.</summary>
public sealed class ConsoleGame
{
    private const string DefaultPdnFile = "partie.pdn";

    private readonly GameSetup _setup;
    private readonly SearchEngine _engine;
    private readonly List<string> _history = [];
    private GameState _state = new();

    public ConsoleGame(GameSetup setup)
    {
        _setup = setup;
        _engine = new SearchEngine(setup.Difficulty);

        if (setup.PdnToOpen is string path)
        {
            OpenPdn(path);
        }
    }

    public void Run()
    {
        PrintIntroduction();

        while (true)
        {
            Draw();

            GameResult result = _state.Result();
            if (result.IsOver())
            {
                Console.WriteLine($"Fin de la partie : {result.ToFrench()}.");
                return;
            }

            bool keepPlaying = _setup.SeatOf(_state.SideToMove) == Seat.Human
                ? PlayHumanTurn()
                : PlayComputerTurn();

            if (!keepPlaying)
            {
                Console.WriteLine("Partie interrompue.");
                return;
            }
        }
    }

    private void PrintIntroduction()
    {
        Console.WriteLine("Dames internationales — damier 10x10, prise obligatoire et maximale, dame volante.");
        Console.WriteLine("Notation officielle : 32-28 pour un déplacement, 32x23 pour une prise.");
        Console.WriteLine("Commandes : 'coups' liste les coups légaux, 'annuler' revient en arrière, 'quitter' abandonne.");
        Console.WriteLine($"            'enregistrer [fichier]' et 'ouvrir <fichier>' échangent la partie en PDN (défaut : {DefaultPdnFile}).");
        Console.WriteLine($"Niveau de l'ordinateur : {SearchOptions.ToFrench(_setup.Difficulty)}.");
        Console.WriteLine();
    }

    private void Draw()
    {
        Console.WriteLine(_state.Board.Render(_setup.ShowNumbers));
        Console.WriteLine("  o = pion blanc   O = dame blanche   x = pion noir   X = dame noire");
        Console.WriteLine($"  Blancs : {_state.Board.CountOf(Player.White)} pièces   |   " +
                          $"Noirs : {_state.Board.CountOf(Player.Black)} pièces");

        if (_history.Count > 0)
        {
            Console.WriteLine($"  Dernier coup : {_history[^1]}");
        }

        Console.WriteLine();
    }

    private bool PlayHumanTurn()
    {
        List<Move> legal = _state.LegalMoves();
        string camp = _state.SideToMove == Player.White ? "Blancs" : "Noirs";

        if (legal.Any(m => m.IsCapture))
        {
            int count = legal[0].CaptureCount;
            Console.WriteLine(count == 1
                ? "Prise obligatoire."
                : $"Prise obligatoire : vous devez enlever {count} pièces.");
        }

        while (true)
        {
            Console.Write($"{camp}, votre coup : ");
            string? input = Console.ReadLine();

            if (input is null)
            {
                return false;
            }

            string trimmed = input.Trim();
            string verb = trimmed.Split(' ', 2)[0].ToLowerInvariant();
            string argument = trimmed.Contains(' ') ? trimmed[(trimmed.IndexOf(' ') + 1)..].Trim() : string.Empty;

            switch (verb)
            {
                case "":
                    continue;
                case "quitter" or "q":
                    return false;
                case "coups" or "?":
                    Console.WriteLine("  " + string.Join("   ", legal.Select(Notation.Describe)));
                    continue;
                case "annuler" or "u":
                    Undo();
                    return true;
                case "enregistrer":
                    SavePdn(argument.Length > 0 ? argument : DefaultPdnFile);
                    continue;
                case "ouvrir":
                    if (argument.Length == 0)
                    {
                        Console.WriteLine($"  Indiquez le fichier à ouvrir, par exemple : ouvrir {DefaultPdnFile}");
                        continue;
                    }

                    if (OpenPdn(argument))
                    {
                        return true;
                    }

                    continue;
            }

            if (!Notation.TryParse(input, legal, out Move? move, out string? error))
            {
                Console.WriteLine($"  {error}");
                continue;
            }

            Apply(move!);
            return true;
        }
    }

    private bool PlayComputerTurn()
    {
        string camp = _state.SideToMove == Player.White ? "Blancs" : "Noirs";
        Console.Write($"{camp} (ordinateur) réfléchit… ");

        SearchResult result = _engine.Search(_state);
        if (result.BestMove is null)
        {
            Console.WriteLine();
            return true;
        }

        Console.WriteLine($"{Notation.Describe(result.BestMove)}  " +
                          $"[profondeur {result.Depth}, {result.Nodes:N0} positions, {result.Elapsed.TotalSeconds:F1} s, " +
                          $"évaluation {FormatScore(result.Score)}]");
        Console.WriteLine();

        Apply(result.BestMove);
        return true;
    }

    private void Apply(Move move)
    {
        string camp = _state.SideToMove == Player.White ? "Blancs" : "Noirs";
        _history.Add($"{camp} {Notation.Describe(move)}");
        _state.MakeMove(move);
    }

    /// <summary>Écrit la partie en cours dans un fichier PDN.</summary>
    private void SavePdn(string path)
    {
        try
        {
            File.WriteAllText(path, PdnFile.Write(_state, [
                new PdnTag("Site", "Dames, en console"),
                new PdnTag("White", NameOf(Player.White)),
                new PdnTag("Black", NameOf(Player.Black)),
            ]));

            Console.WriteLine($"  Partie enregistrée dans {Path.GetFullPath(path)}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  Impossible d'écrire {path} : {error.Message}");
        }
    }

    /// <summary>
    /// Remplace la partie en cours par celle d'un fichier PDN. Tant que le fichier n'est pas
    /// entièrement relu et validé, la partie en cours reste intacte.
    /// </summary>
    private bool OpenPdn(string path)
    {
        try
        {
            PdnGame game = PdnFile.Parse(File.ReadAllText(path));
            GameState loaded = game.ToGameState();

            _state = loaded;
            RebuildHistory();

            string who = game.Tag("Event") is string name and not "?" ? $"« {name} », " : string.Empty;
            Console.WriteLine($"  Partie ouverte : {who}{loaded.PlyCount} demi-coups rejoués.");
            return true;
        }
        catch (Exception error) when (error is PdnException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.WriteLine($"  Impossible d'ouvrir {path} : {error.Message}");
            return false;
        }
    }

    private void RebuildHistory()
    {
        _history.Clear();
        var replay = new GameState(_state.StartingBoard, _state.StartingSide);

        foreach (Move move in _state.History)
        {
            string camp = replay.SideToMove == Player.White ? "Blancs" : "Noirs";
            _history.Add($"{camp} {Notation.Describe(move)}");
            replay.MakeMove(move);
        }
    }

    private string NameOf(Player player) =>
        _setup.SeatOf(player) == Seat.Human
            ? "Humain"
            : $"Ordinateur ({SearchOptions.ToFrench(_setup.Difficulty)})";

    /// <summary>Annule le dernier coup humain, et le coup de l'ordinateur qui le précède le cas échéant.</summary>
    private void Undo()
    {
        int steps = _setup.White == Seat.Human && _setup.Black == Seat.Human ? 1 : 2;
        int done = 0;

        while (done < steps && _state.PlyCount > 0)
        {
            _state.UnmakeMove();
            _history.RemoveAt(_history.Count - 1);
            done++;
        }

        Console.WriteLine(done == 0 ? "  Rien à annuler." : $"  {done} demi-coup(s) annulé(s).");
    }

    private static string FormatScore(int score)
    {
        if (Math.Abs(score) > Evaluator.WinScore - 1000)
        {
            return score > 0 ? "gain forcé" : "perte forcée";
        }

        return $"{score / 100.0:+0.00;-0.00;0.00} pion";
    }
}
