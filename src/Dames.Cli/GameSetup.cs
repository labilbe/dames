using Dames.Core;

namespace Dames.Cli;

/// <summary>Qui tient chaque camp.</summary>
public enum Seat
{
    Human,
    Computer,
}

/// <summary>Configuration d'une partie, lue sur la ligne de commande ou demandée interactivement.</summary>
public sealed record GameSetup(Seat White, Seat Black, Difficulty Difficulty, bool ShowNumbers, string? PdnToOpen = null)
{
    /// <summary>
    /// Analyse les arguments. Renvoie <c>null</c> si l'aide a été demandée ou si un argument est invalide.
    /// Sans argument, la configuration est demandée au joueur.
    /// </summary>
    public static GameSetup? FromArguments(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            return null;
        }

        if (args.Length == 0)
        {
            return AskInteractively();
        }

        Seat white = Seat.Human;
        Seat black = Seat.Computer;
        Difficulty difficulty = Difficulty.Medium;
        bool showNumbers = true;
        string? pdnToOpen = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            switch (arg)
            {
                case "--humain-vs-ia":
                    (white, black) = (Seat.Human, Seat.Computer);
                    break;
                case "--ia-vs-humain":
                    (white, black) = (Seat.Computer, Seat.Human);
                    break;
                case "--humain-vs-humain":
                    (white, black) = (Seat.Human, Seat.Human);
                    break;
                case "--ia-vs-ia":
                    (white, black) = (Seat.Computer, Seat.Computer);
                    break;
                case "--sans-numeros":
                    showNumbers = false;
                    break;
                case "--ouvrir":
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--ouvrir attend le chemin d'un fichier PDN.");
                        return null;
                    }

                    pdnToOpen = args[++i];
                    break;
                case "--niveau":
                    if (i + 1 >= args.Length || !TryParseDifficulty(args[++i], out difficulty))
                    {
                        Console.Error.WriteLine("Niveau inconnu. Valeurs possibles : facile, moyen, difficile, expert.");
                        return null;
                    }

                    break;
                default:
                    Console.Error.WriteLine($"Argument inconnu : {args[i]}");
                    return null;
            }
        }

        return new GameSetup(white, black, difficulty, showNumbers, pdnToOpen);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            Dames internationales (10x10)

            Utilisation :
              dotnet run --project src/Dames.Cli [options]

            Sans option, le programme demande la configuration de la partie.

            Options :
              --humain-vs-ia        Vous jouez les Blancs (par défaut)
              --ia-vs-humain        Vous jouez les Noirs
              --humain-vs-humain    Deux joueurs sur le même clavier
              --ia-vs-ia            Démonstration : l'ordinateur joue les deux camps
              --niveau <n>          facile | moyen | difficile | expert (défaut : moyen)
              --ouvrir <fichier>    Reprend une partie enregistrée au format PDN
              --sans-numeros        Masque les numéros des cases vides
              -h, --help            Affiche cette aide
            """);
    }

    private static GameSetup AskInteractively()
    {
        Console.WriteLine("=== Dames internationales (10x10) ===");
        Console.WriteLine();
        Console.WriteLine("1. Humain (Blancs) contre ordinateur");
        Console.WriteLine("2. Ordinateur contre humain (Noirs)");
        Console.WriteLine("3. Humain contre humain");
        Console.WriteLine("4. Ordinateur contre ordinateur");

        (Seat white, Seat black) = Ask("Votre choix [1] : ", "1") switch
        {
            "2" => (Seat.Computer, Seat.Human),
            "3" => (Seat.Human, Seat.Human),
            "4" => (Seat.Computer, Seat.Computer),
            _ => (Seat.Human, Seat.Computer),
        };

        Difficulty difficulty = Difficulty.Medium;
        if (white == Seat.Computer || black == Seat.Computer)
        {
            Console.WriteLine();
            Console.WriteLine("Niveau : facile, moyen, difficile, expert");
            while (!TryParseDifficulty(Ask("Votre choix [moyen] : ", "moyen"), out difficulty))
            {
                Console.WriteLine("Niveau inconnu.");
            }
        }

        Console.WriteLine();
        return new GameSetup(white, black, difficulty, ShowNumbers: true);
    }

    private static string Ask(string prompt, string fallback)
    {
        Console.Write(prompt);
        string? answer = Console.ReadLine();
        return string.IsNullOrWhiteSpace(answer) ? fallback : answer.Trim();
    }

    private static bool TryParseDifficulty(string value, out Difficulty difficulty)
    {
        switch (value.ToLowerInvariant())
        {
            case "facile" or "easy" or "1":
                difficulty = Difficulty.Easy;
                return true;
            case "moyen" or "medium" or "2":
                difficulty = Difficulty.Medium;
                return true;
            case "difficile" or "hard" or "3":
                difficulty = Difficulty.Hard;
                return true;
            case "expert" or "4":
                difficulty = Difficulty.Expert;
                return true;
            default:
                difficulty = Difficulty.Medium;
                return false;
        }
    }

    public Seat SeatOf(Player player) => player == Player.White ? White : Black;
}
