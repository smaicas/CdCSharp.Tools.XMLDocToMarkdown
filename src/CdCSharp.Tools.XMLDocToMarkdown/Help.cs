using CdCSharp.Cli;

namespace Nj.Tools.XmlDocToMarkdown;
internal static class Help
{
    internal static Task ShowHelp(CommandLinePipe commandLinePipe)
    {
        Dictionary<string, string> parametersHelp = new(){
    {"-h, --help", "Shows help" },
    {"-d, --docs", "Documentation generation" },
    };

        Console.WriteLine();
        foreach (KeyValuePair<string, string> parameter in parametersHelp)
        {
            Console.WriteLine($"{parameter.Key,-40}{parameter.Value}");
        }
        Console.WriteLine();
        return Task.CompletedTask;
    }

    internal static Task ShowDocHelp(CommandLinePipe commandLinePipe)
    {
        Dictionary<string, string> parametersHelp = new(){
    {"-h, --help", "Shows help" },
    {"-d, --docs", "Documentation generation" }
    };

        Console.WriteLine();
        foreach (KeyValuePair<string, string> parameter in parametersHelp)
        {
            Console.WriteLine($"{parameter.Key,-40}{parameter.Value}");
        }
        Console.WriteLine();
        return Task.CompletedTask;
    }
}