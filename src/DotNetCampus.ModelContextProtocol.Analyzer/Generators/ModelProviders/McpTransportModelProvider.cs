using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetCampus.ModelContextProtocol.Generators.ModelProviders;

internal static class McpTransportModelProvider
{
    extension(SyntaxValueProvider syntaxProvider)
    {
        internal IncrementalValuesProvider<McpTransportGeneratingModel> SelectMcpTransport<TAttribute>()
        {
            return syntaxProvider.ForAttributeWithMetadataName(typeof(TAttribute).FullName!,
                    (n, ct) =>
                    {
                        if (n is not ClassDeclarationSyntax cds)
                        {
                            // 必须是类型声明。
                            return false;
                        }

                        if (!cds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                        {
                            // 必须是分部类。
                            return false;
                        }

                        return true;
                    },
                    (c, ct) =>
                    {
                        if (c.SemanticModel.GetDeclaredSymbol(c.TargetNode, ct) is not INamedTypeSymbol typeSymbol)
                        {
                            return null;
                        }

                        return McpTransportGeneratingModel.TryParse(typeSymbol, ct);
                    })
                .Where(t => t is not null)
                .Select((t, ct) => t!);
        }
    }
}
