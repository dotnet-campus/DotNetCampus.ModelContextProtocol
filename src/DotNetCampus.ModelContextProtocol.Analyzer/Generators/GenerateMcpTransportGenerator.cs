using DotNetCampus.ModelContextProtocol.CompilerServices;
using DotNetCampus.ModelContextProtocol.Generators.ModelProviders;
using DotNetCampus.ModelContextProtocol.Generators.Models;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public class GenerateMcpTransportGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var serverTransportProvider = context.SyntaxProvider.SelectMcpTransport<GenerateMcpServerTransportAttribute>();
        var clientTransportProvider = context.SyntaxProvider.SelectMcpTransport<GenerateMcpClientTransportAttribute>();

        context.RegisterSourceOutput(serverTransportProvider, GenerateTransport);
        context.RegisterSourceOutput(clientTransportProvider, GenerateTransport);
    }

    private void GenerateTransport(SourceProductionContext context, McpTransportGeneratingModel model)
    {
    }
}
