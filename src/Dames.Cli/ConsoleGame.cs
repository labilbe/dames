using Dames.Core;

namespace Dames.Cli;

/// <summary>Déroulement d'une partie dans le terminal.</summary>
public sealed class ConsoleGame
{
    private readonly GameSetup _setup;
    private readonly GameState _state = new();
    private readonly SearchEngine _engine;
    private readonly List<string> _history = [];

    public ConsoleGame(GameSetup setup)
    {
        _setup = setup;
        _engine = new SearchEngine(setup.Difficulty);
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

            switch (input.Trim().ToLowerInvariant())
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
