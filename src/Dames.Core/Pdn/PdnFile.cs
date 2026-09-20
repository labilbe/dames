using System.Globalization;
using System.Text;

namespace Dames.Core.Pdn;

/// <summary>
/// Lecture et écriture du format PDN (Portable Draughts Notation), le PGN des dames.
/// Seul le jeu international 10x10 est traité, soit <c>GameType "20"</c>.
/// </summary>
public static class PdnFile
{
    /// <summary>Code PDN du jeu de dames international.</summary>
    public const string InternationalGameType = "20";

    private const int WrapColumn = 80;

    private static readonly string[] SevenTagRoster =
        ["Event", "Site", "Date", "Round", "White", "Black", "Result"];

    /// <summary>
    /// Écrit une partie au format PDN. Les en-têtes fournies sont reprises telles quelles ;
    /// celles du sept-tag roster manquantes sont complétées, et la position de départ n'est
    /// écrite que si elle diffère de la position initiale.
    /// </summary>
    public static string Write(GameState state, IEnumerable<PdnTag>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        List<PdnTag> header = BuildHeader(state, tags);
        var sb = new StringBuilder();

        foreach (PdnTag tag in header)
        {
            sb.Append('[').Append(tag.Name).Append(" \"").Append(Escape(tag.Value)).Append("\"]").Append('\n');
        }

        sb.Append('\n');
        sb.Append(WriteMoveText(state, header));
        sb.Append('\n');
        return sb.ToString();
    }

    /// <summary>Lit la première partie d'un texte PDN.</summary>
    public static PdnGame Parse(string text)
    {
        IReadOnlyList<PdnGame> games = ParseAll(text);
        return games.Count > 0 ? games[0] : throw new PdnException("Aucune partie trouvée dans ce PDN.");
    }

    /// <summary>Lit toutes les parties d'un texte PDN.</summary>
    public static IReadOnlyList<PdnGame> ParseAll(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var games = new List<PdnGame>();
        var builder = new GameBuilder();
        int index = 0;

        while (index < text.Length)
        {
            char c = text[index];

            if (char.IsWhiteSpace(c))
            {
                index++;
            }
            else if (c == '{')
            {
                index = SkipUntil(text, index + 1, '}');
            }
            else if (c == ';')
            {
                while (index < text.Length && text[index] != '\n')
                {
                    index++;
                }
            }
            else if (c == '(')
            {
                index = SkipVariation(text, index);
            }
            else if (c == '[')
            {
                // Une en-tête qui suit des coups ouvre la partie suivante.
                if (builder.HasMoveText)
                {
                    games.Add(builder.Build());
                    builder = new GameBuilder();
                }

                index = ReadTag(text, index, builder);
            }
            else
            {
                index = ReadMoveToken(text, index, builder);
            }
        }

        if (builder.HasContent)
        {
            games.Add(builder.Build());
        }

        return games;
    }

    private static List<PdnTag> BuildHeader(GameState state, IEnumerable<PdnTag>? tags)
    {
        List<PdnTag> given = tags?.ToList() ?? [];
        var header = new List<PdnTag>();

        foreach (string name in SevenTagRoster)
        {
            PdnTag? found = Find(given, name);
            header.Add(new PdnTag(name, found?.Value ?? DefaultTagValue(name, state)));
        }

        header.Add(new PdnTag("GameType", Find(given, "GameType")?.Value ?? InternationalGameType));

        Board start = state.StartingBoard;
        bool standardStart = state.StartingSide == Player.White && IsInitialPosition(start);
        if (!standardStart)
        {
            header.Add(new PdnTag("SetUp", "1"));
            header.Add(new PdnTag("FEN", PdnFen.Write(start, state.StartingSide)));
        }

        // Les en-têtes supplémentaires du client viennent après les en-têtes normalisées.
        foreach (PdnTag tag in given)
        {
            if (!header.Any(h => string.Equals(h.Name, tag.Name, StringComparison.OrdinalIgnoreCase)))
            {
                header.Add(tag);
            }
        }

        return header;
    }

    private static string DefaultTagValue(string name, GameState state) => name switch
    {
        "Event" => "Partie amicale",
        "Site" => "?",
        "Date" => DateTime.Now.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture),
        "Round" => "-",
        "White" or "Black" => "?",
        "Result" => ResultTag(state.Result()),
        _ => "?",
    };

    private static string WriteMoveText(GameState state, List<PdnTag> header)
    {
        var replay = new GameState(state.StartingBoard, state.StartingSide);
        var line = new StringBuilder();
        var sb = new StringBuilder();

        int moveNumber = 1;
        bool blackToOpen = state.StartingSide == Player.Black;

        for (int ply = 0; ply < state.History.Count; ply++)
        {
            Move move = state.History[ply];
            bool whiteToPlay = replay.SideToMove == Player.White;

            if (whiteToPlay || (ply == 0 && blackToOpen))
            {
                Append(sb, line, whiteToPlay ? $"{moveNumber}." : $"{moveNumber}...");
            }

            Append(sb, line, Notate(move, replay.LegalMoves()));
            replay.MakeMove(move);

            if (!whiteToPlay)
            {
                moveNumber++;
            }
        }

        Append(sb, line, Find(header, "Result")?.Value ?? "*");

        if (line.Length > 0)
        {
            sb.Append(line);
        }

        return sb.ToString();
    }

    /// <summary>
    /// La notation abrégée départ-arrivée suffit presque toujours. Quand plusieurs rafles
    /// légales relient les deux mêmes cases, on écrit le chemin complet : c'est la seule
    /// façon de rendre le fichier relisible sans ambiguïté.
    /// </summary>
    private static string Notate(Move move, List<Move> legal)
    {
        int sameEnds = legal.Count(m => m.From == move.From && m.To == move.To);
        return sameEnds > 1 ? move.ToNotation() : move.ToShortNotation();
    }

    private static void Append(StringBuilder sb, StringBuilder line, string token)
    {
        if (line.Length > 0 && line.Length + 1 + token.Length > WrapColumn)
        {
            sb.Append(line).Append('\n');
            line.Clear();
        }

        if (line.Length > 0)
        {
            line.Append(' ');
        }

        line.Append(token);
    }

    private static bool IsInitialPosition(Board board)
    {
        Board initial = Board.CreateInitial();
        for (int n = 1; n <= Squares.PlayableCount; n++)
        {
            if (board.AtNumber(n) != initial.AtNumber(n))
            {
                return false;
            }
        }

        return true;
    }

    private static int SkipUntil(string text, int index, char terminator)
    {
        while (index < text.Length && text[index] != terminator)
        {
            index++;
        }

        return Math.Min(index + 1, text.Length);
    }

    private static int SkipVariation(string text, int index)
    {
        int depth = 0;
        while (index < text.Length)
        {
            char c = text[index++];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')' && --depth == 0)
            {
                break;
            }
        }

        return index;
    }

    private static int ReadTag(string text, int index, GameBuilder builder)
    {
        int close = text.IndexOf(']', index);
        if (close < 0)
        {
            throw new PdnException("En-tête non terminée : il manque un ']'.");
        }

        string body = text[(index + 1)..close].Trim();
        int quote = body.IndexOf('"');
        if (quote < 0)
        {
            throw new PdnException($"En-tête sans valeur entre guillemets : '[{body}]'.");
        }

        string name = body[..quote].Trim();
        string value = body[(quote + 1)..].TrimEnd();
        if (value.EndsWith('"'))
        {
            value = value[..^1];
        }

        builder.AddTag(new PdnTag(name, Unescape(value)));
        return close + 1;
    }

    private static int ReadMoveToken(string text, int index, GameBuilder builder)
    {
        int start = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) && text[index] is not ('{' or '(' or ';' or '['))
        {
            index++;
        }

        string token = text[start..index];
        if (token.Length == 0)
        {
            return index + 1;
        }

        if (token.StartsWith('$'))
        {
            return index; // Annotation numérique, sans effet sur la partie.
        }

        // Un numéro de coup peut être collé au coup : « 1.32-28 ».
        int digits = 0;
        while (digits < token.Length && char.IsAsciiDigit(token[digits]))
        {
            digits++;
        }

        if (digits > 0 && digits < token.Length && token[digits] == '.')
        {
            int dots = digits;
            while (dots < token.Length && token[dots] == '.')
            {
                dots++;
            }

            token = token[dots..];
            if (token.Length == 0)
            {
                return index;
            }
        }

        builder.AddToken(token);
        return index;
    }

    private static string ResultTag(GameResult result) => result switch
    {
        GameResult.WhiteWins => "1-0",
        GameResult.BlackWins => "0-1",
        GameResult.Draw => "1/2-1/2",
        _ => "*",
    };

    internal static GameResult ParseResult(string token) => token switch
    {
        "1-0" or "2-0" => GameResult.WhiteWins,
        "0-1" or "0-2" => GameResult.BlackWins,
        "1/2-1/2" or "1-1" => GameResult.Draw,
        _ => GameResult.InProgress,
    };

    internal static bool IsResultToken(string token) =>
        token is "1-0" or "0-1" or "1/2-1/2" or "*" or "2-0" or "0-2" or "1-1";

    private static PdnTag? Find(IEnumerable<PdnTag> tags, string name)
    {
        foreach (PdnTag tag in tags)
        {
            if (string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }
        }

        return null;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

    /// <summary>Accumule une partie au fil de la lecture et valide chaque coup contre les règles.</summary>
    private sealed class GameBuilder
    {
        private readonly List<PdnTag> _tags = [];
        private readonly List<Move> _moves = [];
        private GameState? _state;
        private GameResult _result = GameResult.InProgress;
        private (Board Board, Player SideToMove)? _setup;

        public bool HasMoveText { get; private set; }

        public bool HasContent => _tags.Count > 0 || HasMoveText;

        public void AddTag(PdnTag tag)
        {
            if (string.Equals(tag.Name, "GameType", StringComparison.OrdinalIgnoreCase))
            {
                string code = tag.Value.Split(',')[0].Trim();
                if (code.Length > 0 && code != InternationalGameType)
                {
                    throw new PdnException(
                        $"Ce PDN décrit un autre jeu (GameType \"{tag.Value}\"). " +
                        $"Seul le jeu international, GameType \"{InternationalGameType}\", est pris en charge.");
                }
            }

            _tags.Add(tag);
        }

        public void AddToken(string token)
        {
            HasMoveText = true;

            if (IsResultToken(token))
            {
                _result = ParseResult(token);
                return;
            }

            GameState state = State();
            if (!Notation.TryParse(token, state.LegalMoves(), out Move? move, out string? error))
            {
                throw new PdnException($"Coup {_moves.Count + 1} du PDN, '{token}' : {error}");
            }

            _moves.Add(move!);
            state.MakeMove(move!);
        }

        public PdnGame Build()
        {
            GameState state = State();
            GameResult declared = _result != GameResult.InProgress ? _result : state.Result();
            (Board board, Player side) = Setup();
            return new PdnGame(_tags, board.Clone(), side, _moves, declared);
        }

        private GameState State()
        {
            if (_state is null)
            {
                (Board board, Player side) = Setup();
                _state = new GameState(board.Clone(), side);
            }

            return _state;
        }

        /// <summary>
        /// La position de départ, lue une seule fois : les en-têtes précèdent toujours les coups,
        /// donc le FEN éventuel est connu avant que le premier coup ne soit joué.
        /// </summary>
        private (Board Board, Player SideToMove) Setup()
        {
            if (_setup is null)
            {
                PdnTag? fen = Find(_tags, "FEN");
                _setup = fen is null
                    ? (Board.CreateInitial(), Player.White)
                    : PdnFen.Parse(fen.Value.Value);
            }

            return _setup.Value;
        }
    }
}
