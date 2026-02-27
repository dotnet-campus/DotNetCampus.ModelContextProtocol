using System.Text;
using DotNetCampus.ModelContextProtocol.CodeAnalysis;
using DotNetCampus.ModelContextProtocol.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DotNetCampus.ModelContextProtocol.Generators;

[Generator(LanguageNames.CSharp)]
public class GenerateMcpTransportGenerator : IIncrementalGenerator
{
    private static readonly Dictionary<string /* FileName */, string /* PackageId */> TransportSourceCodes = new()
    {
        // Key  : dll file name without .dll extension.
        //        We use this to identify the NuGet package uniquely.
        // Value: NuGet Package Id.
        //        We generate one supported transport codes per supported nuget package
        ["dotnetCampus.Ipc"] = "dotnetCampus.Ipc",
        ["TouchSocket.Http"] = "TouchSocket.Http",
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context.AnalyzerConfigOptionsProvider;
        var referencedPackages = context.CompilationProvider
            .Select((c, ct) => FindSupported(c));

        context.RegisterSourceOutput(properties.Combine(referencedPackages), GenerateTransport);
    }

    private static IReadOnlyList<string> FindSupported(Compilation compilation)
    {
        return FindSupportedCore(compilation).ToList();

        static IEnumerable<string> FindSupportedCore(Compilation compilation)
        {
            foreach (var reference in compilation.References)
            {
                var name = Path.GetFileNameWithoutExtension(reference.Display);
                if (name is not null && TransportSourceCodes.ContainsKey(name))
                {
                    yield return name;
                }
            }
        }
    }

    private void GenerateTransport(SourceProductionContext context, (AnalyzerConfigOptionsProvider Left, IReadOnlyList<string> Right) models)
    {
        var (analyzerConfigOptions, referencedPackageIds) = models;
        if (!analyzerConfigOptions.GlobalOptions.TryGetValue("DotNetCampusModelContextProtocolGenerateTransports", out bool generateTransports)
            || generateTransports is false)
        {
            // 如果要求不要生成传输层代码（默认），则不要生成，避免大幅污染目标解决方案里的各种项目。
            return;
        }

        var utf8 = new UTF8Encoding(false);
        foreach (var packageId in referencedPackageIds)
        {
            var prefix = TransportSourceCodes[packageId];
            var sourceFiles = EmbeddedSourceFiles.Enumerate($"Transports/{prefix}").ToList();
            foreach (var sourceFile in sourceFiles)
            {
                context.AddSource($"{packageId}.Mcp/{sourceFile.FileName}", SourceText.From(sourceFile.Content, utf8));
            }
        }
    }
}
