using Dames.Core;
using Dames.Core.Pdn;

namespace Dames.Core.Tests;

public class PdnFenTests
{
    [Fact]
    public void Write_DescribesTheInitialPosition()
    {
        string fen = PdnFen.Write(Board.CreateInitial(), Player.White);

        Assert.StartsWith("W:W31,32,33", fen);
        Assert.Contains(":B1,2,3", fen);
    }

    [Fact]
    public void Write_MarksKingsWithK()
    {
        Board board = Board.FromPositionString("W:K46,31 B:K5");

        // Les cases sont écrites dans l'ordre croissant, quel que soit le type de pièce.
        Assert.Equal("W:W31,K46:BK5", PdnFen.Write(board, Player.White));
    }

    [Fact]
    public void Parse_RoundTripsAPosition()
    {
        Board board = Board.FromPositionString("W:K46,31,32 B:K5,18");

        string fen = PdnFen.Write(board, Player.Black);
        (Board read, Player side) = PdnFen.Parse(fen);

        Assert.Equal(Player.Black, side);
        Assert.Equal(board.Render(), read.Render());
    }

    [Fact]
    public void Parse_AcceptsRangesAndTrailingDot()
    {
        (Board board, Player side) = PdnFen.Parse("W:W31-50:B1-20.");

        Assert.Equal(Player.White, side);
        Assert.Equal(Board.CreateInitial().Render(), board.Render());
    }

    [Theory]
    [InlineData("X:W31:B1", "Trait inconnu")]
    [InlineData("W:W51:B1", "Case invalide")]
    [InlineData("W:Z31:B1", "Camp inconnu")]
    public void Parse_RejectsMalformedInput(string fen, string expected)
    {
        PdnException error = Assert.Throws<PdnException>(() => PdnFen.Parse(fen));

        Assert.Contains(expected, error.Message);
    }
}

public class PdnTests
{
    private static GameState PlayedGame(params string[] notations)
    {
        var state = new GameState();
        foreach (string notation in notations)
        {
            Assert.True(Notation.TryParse(notation, state.LegalMoves(), out Move? move, out string? error), error);
            state.MakeMove(move!);
        }

        return state;
    }

    [Fact]
    public void Write_ProducesTheStandardHeaderAndMoveText()
    {
        GameState state = PlayedGame("32-28", "18-23", "37-32");

        string pdn = PdnFile.Write(state, [new PdnTag("Event", "Test"), new PdnTag("White", "Franck")]);

        Assert.Contains("[Event \"Test\"]", pdn);
        Assert.Contains("[White \"Franck\"]", pdn);
        Assert.Contains("[GameType \"20\"]", pdn);
        Assert.Contains("1. 32-28 18-23 2. 37-32 *", pdn);
        Assert.DoesNotContain("[FEN", pdn);
    }

    [Fact]
    public void Write_UsesShortNotationForCaptures()
    {
        GameState state = PlayedGame("32-28", "19-23", "28x19", "14x23");

        string pdn = PdnFile.Write(state);

        Assert.Contains("1. 32-28 19-23 2. 28x19 14x23", pdn);
    }

    [Fact]
    public void Write_FallsBackToTheFullPathWhenTheShortFormIsAmbiguous()
    {
        // Deux rafles de trois pièces relient 42 à 34, mais n'emportent pas les mêmes pièces :
        // 42x31x18x34 prend 27, 29 et 37, tandis que 42x26x12x34 prend 17, 29 et 37.
        // Écrire « 42x34 » rendrait la partie impossible à rejouer.
        const string position = "W:K42 B:17,K27,29,32,K37,41";
        var state = new GameState(Board.FromPositionString(position), Player.White);

        List<Move> legal = state.LegalMoves();
        Move chosen = legal.Single(m => m.ToNotation() == "42x31x18x34");
        Assert.Equal(2, legal.Count(m => m.From == chosen.From && m.To == chosen.To));
        state.MakeMove(chosen);

        string pdn = PdnFile.Write(state);

        Assert.Contains("42x31x18x34", pdn);
        Assert.Equal(chosen, Assert.Single(PdnFile.Parse(pdn).Moves));
    }

    [Fact]
    public void Write_RecordsANonStandardStartingPosition()
    {
        var state = new GameState(Board.FromPositionString("W:K46 B:23"), Player.White);

        string pdn = PdnFile.Write(state);

        Assert.Contains("[SetUp \"1\"]", pdn);
        Assert.Contains("[FEN \"W:WK46:B23\"]", pdn);
    }

    [Fact]
    public void Write_NumbersFromOneWhenBlackOpens()
    {
        var state = new GameState(Board.CreateInitial(), Player.Black);
        Assert.True(Notation.TryParse("18-23", state.LegalMoves(), out Move? first, out _));
        state.MakeMove(first!);
        Assert.True(Notation.TryParse("32-28", state.LegalMoves(), out Move? second, out _));
        state.MakeMove(second!);

        string pdn = PdnFile.Write(state);

        Assert.Contains("1... 18-23 2. 32-28", pdn);
    }

    [Fact]
    public void WriteThenParse_RoundTripsAWholeGame()
    {
        var original = new GameState();
        var engine = new SearchEngine(SearchOptions.For(Difficulty.Easy) with { RandomSeed = 5 });
        while (!original.Result().IsOver() && original.PlyCount < 60)
        {
            SearchResult result = engine.Search(original);
            original.MakeMove(result.BestMove!);
        }

        string pdn = PdnFile.Write(original);
        PdnGame read = PdnFile.Parse(pdn);
        GameState replayed = read.ToGameState();

        Assert.Equal(original.PlyCount, read.Moves.Count);
        Assert.Equal(original.Board.Render(), replayed.Board.Render());
        Assert.Equal(original.SideToMove, replayed.SideToMove);
        Assert.Equal(pdn, PdnFile.Write(replayed, read.Tags));
    }

    [Fact]
    public void Parse_ReadsHeadersMoveNumbersAndResult()
    {
        const string pdn = """
            [Event "Championnat"]
            [White "Dupont"]
            [Black "Martin"]
            [Result "1-0"]
            [GameType "20"]

            1. 32-28 19-23 2. 28x19 14x23 1-0
            """;

        PdnGame game = PdnFile.Parse(pdn);

        Assert.Equal("Championnat", game.Tag("Event"));
        Assert.Equal("Dupont", game.Tag("White"));
        Assert.Equal(GameResult.WhiteWins, game.Result);
        Assert.Equal(4, game.Moves.Count);
        Assert.Equal("28x19", game.Moves[2].ToShortNotation());
    }

    [Fact]
    public void Parse_IgnoresCommentsVariationsAndAnnotations()
    {
        const string pdn = """
            [Event "Commenté"]

            1. 32-28 {un début tranquille} 19-23 $1 ; ceci est une note
            2. 28x19 (2. 33-29 une autre idée) 14x23 *
            """;

        PdnGame game = PdnFile.Parse(pdn);

        Assert.Equal(4, game.Moves.Count);
        Assert.Equal(GameResult.InProgress, game.Result);
    }

    [Fact]
    public void Parse_AcceptsMoveNumbersGluedToTheMove()
    {
        PdnGame game = PdnFile.Parse("1.32-28 19-23 2.28x19 14x23 *");

        Assert.Equal(4, game.Moves.Count);
    }

    [Fact]
    public void Parse_StartsFromTheFenWhenOneIsGiven()
    {
        const string pdn = """
            [SetUp "1"]
            [FEN "W:WK46:B23"]

            1. 46x5 *
            """;

        PdnGame game = PdnFile.Parse(pdn);

        Assert.Equal(Square.WhiteKing, game.StartingBoard.AtNumber(46));
        Move move = Assert.Single(game.Moves);
        Assert.Equal(1, move.CaptureCount);
        Assert.Equal(Square.WhiteKing, game.ToGameState().Board.AtNumber(5));
    }

    [Fact]
    public void Parse_AcceptsTheMatchPointResultsUsedInDraughts()
    {
        Assert.Equal(GameResult.WhiteWins, PdnFile.Parse("1. 32-28 2-0").Result);
        Assert.Equal(GameResult.Draw, PdnFile.Parse("1. 32-28 1-1").Result);
    }

    [Fact]
    public void ParseAll_ReadsSeveralGamesFromOneFile()
    {
        const string pdn = """
            [Event "Ronde 1"]

            1. 32-28 19-23 *

            [Event "Ronde 2"]

            1. 33-28 18-22 *
            """;

        IReadOnlyList<PdnGame> games = PdnFile.ParseAll(pdn);

        Assert.Equal(2, games.Count);
        Assert.Equal("Ronde 1", games[0].Tag("Event"));
        Assert.Equal("Ronde 2", games[1].Tag("Event"));
    }

    [Fact]
    public void Parse_RejectsAnIllegalMoveAndSaysWhich()
    {
        PdnException error = Assert.Throws<PdnException>(() => PdnFile.Parse("1. 32-28 19-23 2. 33-29 *"));

        // La prise 28x19 est obligatoire : 33-29 ne peut pas être le troisième coup.
        Assert.Contains("Coup 3", error.Message);
        Assert.Contains("33-29", error.Message);
    }

    [Fact]
    public void Parse_RejectsAnotherDraughtsVariant()
    {
        PdnException error = Assert.Throws<PdnException>(() => PdnFile.Parse("[GameType \"21\"]\n\n1. 11-15 *"));

        Assert.Contains("GameType", error.Message);
    }

    [Fact]
    public void Write_WrapsLongGamesInsteadOfProducingOneEndlessLine()
    {
        var state = new GameState();
        var engine = new SearchEngine(SearchOptions.For(Difficulty.Easy) with { RandomSeed = 11 });
        while (!state.Result().IsOver() && state.PlyCount < 40)
        {
            state.MakeMove(engine.Search(state).BestMove!);
        }

        string pdn = PdnFile.Write(state);

        Assert.All(pdn.Split('\n'), line => Assert.True(line.Length <= 80, $"Ligne trop longue : {line}"));
    }
}
