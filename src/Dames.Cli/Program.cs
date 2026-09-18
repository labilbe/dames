using System.Text;
using Dames.Cli;
using Dames.Core;

Console.OutputEncoding = Encoding.UTF8;

GameSetup? setup = GameSetup.FromArguments(args);
if (setup is null)
{
    GameSetup.PrintUsage();
    return 0;
}

new ConsoleGame(setup).Run();
return 0;
