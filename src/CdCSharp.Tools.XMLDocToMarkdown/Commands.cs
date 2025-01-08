using CdCSharp.Cli;

namespace Nj.Tools.XmlDocToMarkdown;
internal static class Commands
{
    internal static async Task DocsCommand(CommandLinePipe commandPipe)
    {
        Dictionary<string, Func<CommandLinePipe, Task>> commandParameters = new()
    {
        {"", Commands.GenerateMdDoc },
        { "-h", Help.ShowDocHelp},
        { "--help", Help.ShowDocHelp},
    };

        await commandPipe.ExecuteFirstAsync(commandParameters);
    }

    internal static async Task GenerateMdDoc(CommandLinePipe commandParser)
    {
        string? rootPath = commandParser.GetArgumentWithRequiredValueOrDefault("-p", "--path");
        string? outputFolder = commandParser.GetArgumentWithRequiredValueOrDefault("-o", "--output");
        string? uri = commandParser.GetArgumentWithRequiredValueOrDefault("-u", "--uri");

        rootPath ??= ".";
        outputFolder ??= "docs";

        await MdDocGenerator.GenerateMdDocs(rootPath, outputFolder, uri);
    }
}
