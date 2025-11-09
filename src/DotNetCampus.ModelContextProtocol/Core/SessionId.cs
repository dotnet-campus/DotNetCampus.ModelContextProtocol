using System.Security.Cryptography;

namespace DotNetCampus.ModelContextProtocol.Core;

public readonly record struct SessionId(string Id)
{
    public override string ToString()
    {
        return Id;
    }

    private const int StackAllocThreshold = 128;

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

    public static int GetArraySizeRequiredToEncode(int count)
    {
        var numWholeOrPartialInputBlocks = checked(count + 2) / 3;
        return checked(numWholeOrPartialInputBlocks * 4);
    }
}
