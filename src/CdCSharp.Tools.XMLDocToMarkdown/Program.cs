using CdCSharp.FluentCli;
using CdCSharp.FluentCli.Abstractions;
using Nj.Tools.XmlDocToMarkdown;

try
{
    FCli cli = new FCli()
            .WithDescription("XML Documentation to Markdown Generator")
            .WithErrorHandler(ex => Console.WriteLine(ex.Message))
            .Command<DocsArgs>("docs")
                .WithAlias("d")
                .WithDescription("Generate markdown documentation")
                .OnExecute(async args =>
                    await MdDocGenerator.GenerateMdDocs(
                        rootPath: args.Path,
                        outputFolder: args.Output,
                        crefsUri: args.Uri));

    await cli.ExecuteAsync(args);

}
catch (ArgumentException ex)
{
    Console.WriteLine(ex.Message);
}

public class DocsArgs
{
    [Arg("path", "Root folder", "p")]
    public string Path { get; set; } = ".";

    [Arg("output", "Output folder", "o")]
    public string Output { get; set; } = "docs";

    [Arg("uri", "Base URI for references", "u")]
    public string? Uri { get; set; }
}