using System.Text.Json;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 回声测试工具，用于测试复杂对象的参数传递。
/// </summary>
public class EchoTool
{
    /// <summary>
    /// 回声简单字符串。
    /// </summary>
    /// <param name="message">要回声的消息。</param>
    /// <returns>原样返回消息。</returns>
    [McpServerTool(ReadOnly = true)]
    public string Echo(string message)
    {
        return message;
    }

    /// <summary>
    /// 回声用户对象。
    /// </summary>
    /// <param name="user">用户信息。</param>
    /// <returns>序列化后的用户信息 JSON 字符串。</returns>
    [McpServerTool(ReadOnly = true)]
    public string EchoUser(EchoUserInfo user)
    {
        return JsonSerializer.Serialize(user);
    }
}

/// <summary>
/// 用于测试复杂对象传参的用户信息。
/// </summary>
public record EchoUserInfo
{
    /// <summary>
    /// 用户名。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 用户 ID。
    /// </summary>
    public required int Id { get; init; }
}
