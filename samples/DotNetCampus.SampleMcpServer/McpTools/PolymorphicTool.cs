using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.SampleMcpServer.McpTools;

public class PolymorphicTool
{
    /// <summary>
    /// 测试多态参数的工具方法
    /// </summary>
    /// <param name="param">多态参数</param>
    /// <returns>处理结果字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string TestPolymorphicParameter(PolymorphicBase param)
    {
        return param switch
        {
            PolymorphicDerivedA a => $"Received DerivedA with Foo = {a.Foo}",
            PolymorphicDerivedB b => $"Received DerivedB with Bar = {b.Bar}",
            _ => "Unknown type",
        };
    }

    /// <summary>
    /// 测试整个输入对象作为多态参数的工具方法
    /// </summary>
    /// <param name="param">多态参数</param>
    /// <returns>处理结果字符串</returns>
    [McpServerTool(ReadOnly = true)]
    public string TestPolymorphicInput(
        [ToolParameter(Type = ToolParameterType.InputObject)]
        PolymorphicBase param)
    {
        return param switch
        {
            PolymorphicDerivedA a => $"Received DerivedA with Foo = {a.Foo}",
            PolymorphicDerivedB b => $"Received DerivedB with Bar = {b.Bar}",
            _ => "Unknown type",
        };
    }
}

[JsonDerivedType(typeof(PolymorphicDerivedA), "a")]
[JsonDerivedType(typeof(PolymorphicDerivedB), "b")]
[JsonDerivedType(typeof(PolymorphicDerivedC), "c")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public record PolymorphicBase
{
}

public record PolymorphicDerivedA : PolymorphicBase
{
    public string? Foo { get; init; }
}

public record PolymorphicDerivedB : PolymorphicBase
{
    public int? Bar { get; init; }
}

public record PolymorphicDerivedC : PolymorphicBase
{
    public JsonElement? Baz { get; init; }
}
