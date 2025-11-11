using System.Text.Json;

namespace DotNetCampus.ModelContextProtocol.Core;

public readonly record struct EmptyResult
{
    public static JsonElement JsonElement { get; } = JsonDocument.Parse("{}").RootElement;
}
