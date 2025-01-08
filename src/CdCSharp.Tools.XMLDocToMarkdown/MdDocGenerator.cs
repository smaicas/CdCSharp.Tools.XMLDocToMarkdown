using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Nj.Tools.XmlDocToMarkdown;
internal sealed class MdDocGenerator
{
    private const bool ShowPrivates = false;
    private const bool ShowGetters = false;
    private const bool ShowSetters = false;

    private static readonly List<(string FileName, INamedTypeSymbol Symbol)> KnownSymbols = [];
    private static string? CrefsUri = null;

    public static async Task GenerateMdDocs(string rootPath, string outputFolder, string? crefsUri = null)
    {
        CrefsUri = BuildUriFormat(crefsUri);

        IEnumerable<string> projectFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly);
        string? projectFile = projectFiles.FirstOrDefault() ?? throw new InvalidOperationException("Project file not found");

        bool relativeToRoot = false == outputFolder.Contains(":");

        string outputPath = relativeToRoot ? Path.Combine(rootPath, outputFolder) : outputFolder;

        Compilation compilation = await CreateCompilationFromProject(projectFile);

        foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            SyntaxNode root = await syntaxTree.GetRootAsync();

            foreach (ClassDeclarationSyntax classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol symbol)
                {
                    KnownSymbols.Add(
                        (
                        $"{symbol.GetFullNamespaceName()}.{symbol.Name}.md"
                        , symbol
                        ));
                }
            }
        }

        foreach ((string fileName, INamedTypeSymbol symbol) in KnownSymbols)
        {
            string markdown = GenerateMarkdownDocumentation(symbol);

            if (!Directory.Exists(outputPath)) { Directory.CreateDirectory(outputPath); }

            string finalFilePath = Path.Combine(outputPath, fileName);
            File.WriteAllText(finalFilePath, markdown);
            Console.WriteLine($"Generated {finalFilePath}");
        }
    }

    private static string? BuildUriFormat(string? crefsUri)
    {
        if (crefsUri == null) { return null; }
        if (!crefsUri.StartsWith("/"))
        {
            crefsUri = $"/{crefsUri}";
        }
        if (crefsUri.EndsWith("/"))
        {
            crefsUri = crefsUri[..^1];
        }
        return crefsUri;
    }

    static async Task<Compilation> CreateCompilationFromProject(string projectFilePath)
    {
        MSBuildWorkspace msBuildWorkspace = MSBuildWorkspace.Create();
        Project project = await msBuildWorkspace.OpenProjectAsync(projectFilePath);
        Compilation? compilation = await project.GetCompilationAsync();
        return compilation!;
    }

    static string GenerateMarkdownDocumentation(INamedTypeSymbol symbol)
    {
        StringBuilder sb = new();
        sb.AppendLine($"# {symbol.Name}");
        sb.AppendLine();
        sb.AppendLine($"*Namespace:* {symbol.GetFullNamespaceName()}");
        sb.AppendLine($"*Assembly:* {symbol.ContainingAssembly.Name}");
        string? filePath = symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath;
        if (filePath != null)
        {
            sb.AppendLine($"*Source:* {filePath.Split("\\").Last()}");
        }
        sb.AppendLine();
        sb.AppendLine();

        string? xmlDocumentation = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlDocumentation))
        {
            try
            {
                ExtractSummaryFromDocumentation(sb, xmlDocumentation);
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Can't extract documentation for element {symbol.Name}. Exception {ex.Message}");
            }
        }

        sb.AppendLine($"---");
        IncludeMembers(sb, symbol);

        INamedTypeSymbol? baseSymbol = symbol.BaseType;
        while (baseSymbol != null && baseSymbol.SpecialType != SpecialType.System_Object)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## Inherited from {baseSymbol.Name}");
            sb.AppendLine();

            string? baseXmlDocumentation = baseSymbol.GetDocumentationCommentXml();
            if (!string.IsNullOrEmpty(baseXmlDocumentation))
            {
                sb.AppendLine($"**Summary:**");

                try
                {
                    ExtractSummaryFromDocumentation(sb, baseXmlDocumentation);
                }
                catch (XmlException ex)
                {
                    Console.WriteLine($"Can't extract documentation for element {symbol.Name}. Exception {ex.Message}");
                }
            }

            sb.AppendLine($"---");
            IncludeMembers(sb, baseSymbol);
            baseSymbol = baseSymbol.BaseType;
        }

        return sb.ToString();
    }

    static void ExtractSummaryFromDocumentation(StringBuilder sb, string xmlDocumentation)
    {
        XDocument xDoc = XDocument.Parse(xmlDocumentation);
        XElement? summaryElement = xDoc.Descendants("summary").FirstOrDefault();
        if (summaryElement != null)
        {
            ExtractSummaryFromElement(sb, summaryElement);
            sb.AppendLine();
        }
    }

    static void ExtractSummaryFromElement(StringBuilder sb, XElement element)
    {
        foreach (XNode node in element.Nodes())
        {
            if (node is XElement elementNode)
            {
                if (elementNode.Name == "see")
                {
                    // Handle <see> elements
                    string? cref = elementNode.Attribute("cref")?.Value;
                    if (!string.IsNullOrEmpty(cref))
                    {
                        string refClassName = cref.Split(':')[1];
                        if (KnownSymbols.Any(ks => $"{ks.Symbol.GetFullNamespaceName()}.{ks.Symbol.Name}" == refClassName))
                        {
                            if (CrefsUri != null)
                            {
                                sb.Append($"[{refClassName}]({CrefsUri}/{refClassName})");
                            }
                            else
                            {
                                sb.Append($"[{refClassName}](#{refClassName}.md)");
                            }

                        }
                        else
                        {
                            sb.Append($"[{cref}]");
                        }
                    }
                }
                else
                {
                    // Handle other elements
                    ExtractSummaryFromElement(sb, elementNode);
                }
            }
            else if (node is XText textNode)
            {
                // Handle text nodes
                sb.Append(textNode.Value);
            }
        }
    }

    static void IncludeMembers(StringBuilder sb, INamedTypeSymbol symbol)
    {
        foreach (ISymbol member in symbol.GetMembers())
        {
            if (member is IMethodSymbol methodSymbol)
            {
                IncludeMethodDocumentation(sb, methodSymbol);
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                IncludePropertyDocumentation(sb, propertySymbol);
            }
        }
    }

    static void IncludeMethodDocumentation(StringBuilder sb, IMethodSymbol methodSymbol)
    {
        if (ShowPrivates == false && methodSymbol.DeclaredAccessibility == Accessibility.Private) { return; }

        if (ShowGetters == false && methodSymbol.MethodKind == MethodKind.PropertyGet) { return; }
        if (ShowSetters == false && methodSymbol.MethodKind == MethodKind.PropertySet) { return; }

        string? memberXmlDocumentation = methodSymbol.GetDocumentationCommentXml();
        IEnumerable<string> parameters = methodSymbol.Parameters.Select(p => $"{p.Type.Name} {p.Name}");
        string methodSignature = $"`{methodSymbol.ReturnType.Name} {methodSymbol.Name}({string.Join(", ", parameters)})`";

        sb.AppendLine();
        sb.AppendLine($"**Method:** `{methodSymbol.Name}`");
        sb.AppendLine($"*Method Signature:* {methodSignature}");
        if (!string.IsNullOrEmpty(memberXmlDocumentation))
        {
            sb.AppendLine();

            try
            {
                ExtractSummaryFromDocumentation(sb, memberXmlDocumentation);
                sb.AppendLine();
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Can't extract documentation for element {methodSymbol.Name}. Exception {ex.Message}");
            }

        }
        sb.AppendLine();
    }

    static void IncludePropertyDocumentation(StringBuilder sb, IPropertySymbol propertySymbol)
    {
        if (ShowPrivates == false && propertySymbol.DeclaredAccessibility == Accessibility.Private) { return; }

        string? memberXmlDocumentation = propertySymbol.GetDocumentationCommentXml();

        string propertyName = propertySymbol.Name;
        string propertyType = propertySymbol.Type.Name;
        bool nullable = propertySymbol.IsNullable();
        string? initialValue = propertySymbol.GetDefaultValue();

        string[] attributes = propertySymbol.GetAttributes().Select(a => $"[{a.AttributeClass?.Name}]").ToArray();
        sb.AppendLine();
        sb.AppendLine($"**Property:** `{propertyName}` ({propertySymbol.DeclaredAccessibility})");

        if (!string.IsNullOrEmpty(memberXmlDocumentation))
        {
            sb.AppendLine();

            try
            {
                ExtractSummaryFromDocumentation(sb, memberXmlDocumentation);
                sb.AppendLine();
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Can't extract documentation for element {propertySymbol.Name}. Exception {ex.Message}");
            }

        }

        sb.AppendLine($"*Property Type:* `{propertyType}`");

        if (initialValue != null)
        {
            sb.AppendLine($"*Default:* `{initialValue}`");
        }

        sb.AppendLine($"*Nullable:* {nullable}");
        sb.AppendLine($"*Attributes:* {string.Join(", ", attributes)}");
        sb.AppendLine();
    }
}

public static class INamedTypeSymbolExtensions
{
    public static string GetFullNamespaceName(this INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
        {
            throw new ArgumentNullException(nameof(typeSymbol));
        }
        return typeSymbol.ContainingNamespace.ToString() ?? string.Empty;
    }
}

public static class IPropertySymbolExtensions
{
    public static bool IsNullable(this IPropertySymbol propertySymbol) => propertySymbol.Type.NullableAnnotation == NullableAnnotation.Annotated;

    public static string? GetDefaultValue(this IPropertySymbol propertySymbol)
    {
        PropertyDeclarationSyntax? propertyDeclaration = null;
        if (propertySymbol.DeclaringSyntaxReferences.Length > 0)
        {
            propertyDeclaration = propertySymbol.DeclaringSyntaxReferences[0].GetSyntax() as PropertyDeclarationSyntax;
        }

        string? initialValue = null;
        if (propertyDeclaration != null)
        {
            EqualsValueClauseSyntax? initializer = propertyDeclaration.Initializer;
            if (initializer != null)
            {
                initialValue = initializer.Value.ToString();
            }
        }
        return initialValue;
    }
}