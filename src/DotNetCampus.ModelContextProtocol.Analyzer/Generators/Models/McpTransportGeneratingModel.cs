using DotNetCampus.ModelContextProtocol.CompilerServices;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Generators.Models;

public record McpTransportGeneratingModel
{
    public static McpTransportGeneratingModel? TryParse(INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
    {
        var serverId = (McpServerTransportPackageId?)typeSymbol.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(GenerateMcpServerTransportAttribute).FullName)?
            .ConstructorArguments[0].Value;
        var clientId = (McpClientTransportPackageId?)typeSymbol.GetAttributes()
            .FirstOrDefault(x => x.AttributeClass?.ToDisplayString() == typeof(GenerateMcpClientTransportAttribute).FullName)?
            .ConstructorArguments[0].Value;

        if (serverId is null && clientId is null)
        {
            return null;
        }

        return new McpTransportGeneratingModel
        {
            Namespace = typeSymbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName = typeSymbol.Name,
            ServerId = serverId,
            ClientId = clientId,
        };
    }

    /// <summary>
    /// MCP 传输层用户定义的分布类所在的命名空间。
    /// </summary>
    public required string Namespace { get; init; }

    /// <summary>
    /// MCP 传输层用户定义的分布类名称。
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 如果用户期望生成 MCP 服务端传输层，那么此项不为 <see langword="null"/>，并表示期望生成的 MCP 服务端传输层的包 Id。
    /// </summary>
    public required McpServerTransportPackageId? ServerId { get; init; }

    /// <summary>
    /// 如果用户期望生成 MCP 客户端传输层，那么此项不为 <see langword="null"/>，并表示期望生成的 MCP 客户端传输层的包 Id。
    /// </summary>
    public required McpClientTransportPackageId? ClientId { get; init; }
}
