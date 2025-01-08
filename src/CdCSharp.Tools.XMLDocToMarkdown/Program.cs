using CdCSharp.Cli;
using Nj.Tools.XmlDocToMarkdown;

try
{
    CommandLinePipe commandPipe = new(args);

    Dictionary<string, Func<CommandLinePipe, Task>> parameterProcess = new()
    {
        { "--d", Commands.DocsCommand},
        { "--docs", Commands.DocsCommand},
        { "-h", Help.ShowHelp},
        { "--help", Help.ShowHelp},
    };

    await commandPipe.ExecuteFirstAsync(parameterProcess, "Required command arguments. Use nj-xmltomd --help to see help.");

}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
}