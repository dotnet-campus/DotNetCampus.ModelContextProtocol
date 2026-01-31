using System.Text;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.McpTools;

/// <summary>
/// 长文本测试工具，用于测试大数据量返回的场景。
/// </summary>
public class LongTextTool
{
    /// <summary>
    /// 生成指定长度的文本。
    /// </summary>
    /// <param name="length">要生成的字符数量。</param>
    /// <returns>指定长度的重复文本。</returns>
    [McpServerTool(ReadOnly = true)]
    public string Generate(int length)
    {
        const string pattern = "ABCDEFGHIJ";
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(pattern[i % pattern.Length]);
        }
        return sb.ToString();
    }
}
