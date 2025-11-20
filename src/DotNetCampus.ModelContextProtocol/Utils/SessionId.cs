using System.Security.Cryptography;

namespace DotNetCampus.ModelContextProtocol.Utils;

/// <summary>
/// 表示 MCP 会话的唯一标识符。
/// </summary>
/// <param name="Id">会话 ID 字符串</param>
internal readonly record struct SessionId(string Id)
{
    /// <summary>
    /// 获取会话 ID 字符串。
    /// </summary>
    public string Id { get; } = Id;

    /// <summary>
    /// 返回会话 ID 的字符串表示形式。
    /// </summary>
    public override string ToString()
    {
        return Id;
    }

    private const int StackAllocThreshold = 128;

    /// <summary>
    /// 生成一个新的会话 ID。
    /// </summary>
    public static SessionId MakeNew()
    {
        Span<byte> input = stackalloc byte[16];
        RandomNumberGenerator.Fill(input);
        var id = Base64UrlEncode(input);
        return new SessionId(id);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> input)
    {
        if (input.IsEmpty)
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[StackAllocThreshold];
        var numBase64Chars = Base64UrlEncode(input, buffer);
        var base64Url = new string(buffer[..numBase64Chars]);

        return base64Url;
    }

    private static int Base64UrlEncode(ReadOnlySpan<byte> input, Span<char> output)
    {
        if (input.IsEmpty)
        {
            return 0;
        }

        // Use base64url encoding with no padding characters. See RFC 4648, Sec. 5.
        Convert.TryToBase64Chars(input, output, out var charsWritten);

        // Fix up '+' -> '-' and '/' -> '_'. Drop padding characters.
        for (var i = 0; i < charsWritten; i++)
        {
            var ch = output[i];
            if (ch == '+')
            {
                output[i] = '-';
            }
            else if (ch == '/')
            {
                output[i] = '_';
            }
            else if (ch == '=')
            {
                // We've reached a padding character; truncate the remainder.
                return i;
            }
        }

        return charsWritten;
    }

    /// <summary>
    /// 计算编码指定字节数所需的数组大小。
    /// </summary>
    /// <param name="count">要编码的字节数</param>
    public static int GetArraySizeRequiredToEncode(int count)
    {
        var numWholeOrPartialInputBlocks = checked(count + 2) / 3;
        return checked(numWholeOrPartialInputBlocks * 4);
    }
}
