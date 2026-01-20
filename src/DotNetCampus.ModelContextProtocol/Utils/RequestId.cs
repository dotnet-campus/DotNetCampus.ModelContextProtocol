using System.Globalization;
using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Utils;

/// <summary>
/// 表示 MCP 请求的唯一标识符。
/// </summary>
/// <param name="Id">请求 ID。</param>
public readonly record struct RequestId(long Id)
{
    private static long _requestIdCounter;

    /// <summary>
    /// 获取请求 ID。
    /// </summary>
    public long Id { get; } = Id;

    /// <summary>
    /// 返回请求 ID 的 <see cref="JsonElement"/> 对象用于后续 JSON 序列化。
    /// </summary>
    public JsonElement ToJsonElement()
    {
        return JsonSerializer.SerializeToElement(Id, CompiledSchemaJsonContext.Default.Int64);
    }

    /// <summary>
    /// 返回请求 ID 的字符串表示形式。
    /// </summary>
    public override string ToString()
    {
        return Id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 生成一个新的请求 ID。
    /// </summary>
    public static RequestId MakeNew()
    {
        var id = Interlocked.Increment(ref _requestIdCounter);
        return new RequestId(id);
    }
}
