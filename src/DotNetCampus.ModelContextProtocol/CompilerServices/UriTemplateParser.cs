using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Servers;

namespace DotNetCampus.ModelContextProtocol.CompilerServices;

/// <summary>
/// URI 模板解析器，用于简化生成代码中的 URI 解析逻辑。<br/>
/// URI template parser to simplify URI parsing logic in generated code.
/// </summary>
public ref struct UriTemplateParser
{
    private readonly IMcpServerReadResourceContext _context;
    private readonly ReadOnlySpan<char> _uri;
    private int _position;

    /// <summary>
    /// 初始化 URI 模板解析器。<br/>
    /// Initializes URI template parser.
    /// </summary>
    /// <param name="context">资源读取上下文。Resource read context.</param>
    public UriTemplateParser(IMcpServerReadResourceContext context)
    {
        _context = context;
        _uri = context.Uri.AsSpan();
        _position = 0;
    }

    /// <summary>
    /// 匹配静态段。如果不匹配则抛出 <see cref="McpResourceNotFoundException"/>。<br/>
    /// Matches a static segment. Throws <see cref="McpResourceNotFoundException"/> if not matched.
    /// </summary>
    /// <param name="expectedSegment">期望的静态段内容。Expected static segment content.</param>
    public void MatchStaticSegment(string expectedSegment)
    {
        var segment = expectedSegment.AsSpan();
        var remaining = _uri.Slice(_position);

        if (!remaining.StartsWith(segment))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += segment.Length;
    }

    /// <summary>
    /// 解析 <see cref="int"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses an <see cref="int"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的整数值。Parsed integer value.</returns>
    public int ParseInt32Parameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!int.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 解析 <see cref="long"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="long"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的长整数值。Parsed long value.</returns>
    public long ParseInt64Parameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!long.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 解析 <see cref="string"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="string"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的字符串值。Parsed string value.</returns>
    public string ParseStringParameter()
    {
        var paramSpan = ExtractParameterSpan();
        _position += paramSpan.Length;
        return paramSpan.ToString();
    }

    /// <summary>
    /// 解析 <see cref="Guid"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="Guid"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的 GUID 值。Parsed GUID value.</returns>
    public Guid ParseGuidParameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!Guid.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 解析 <see cref="bool"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="bool"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的布尔值。Parsed boolean value.</returns>
    public bool ParseBooleanParameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!bool.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 解析 <see cref="double"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="double"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的双精度浮点数值。Parsed double value.</returns>
    public double ParseDoubleParameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!double.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 解析 <see cref="decimal"/> 类型的参数（读取到下一个 '/' 或 URI 末尾）。<br/>
    /// Parses a <see cref="decimal"/> parameter (reads until next '/' or end of URI).
    /// </summary>
    /// <returns>解析得到的十进制数值。Parsed decimal value.</returns>
    public decimal ParseDecimalParameter()
    {
        var paramSpan = ExtractParameterSpan();

        if (!decimal.TryParse(paramSpan, out var value))
        {
            throw new McpResourceNotFoundException(_context);
        }

        _position += paramSpan.Length;
        return value;
    }

    /// <summary>
    /// 确保已到达 URI 末尾。如果未到达末尾则抛出 <see cref="McpResourceNotFoundException"/>。<br/>
    /// Ensures the end of URI has been reached. Throws <see cref="McpResourceNotFoundException"/> if not at the end.
    /// </summary>
    public void EnsureEndOfUri()
    {
        if (_position < _uri.Length)
        {
            throw new McpResourceNotFoundException(_context);
        }
    }

    /// <summary>
    /// 提取参数片段（到下一个 '/' 或 URI 末尾）。<br/>
    /// Extracts parameter span (until next '/' or end of URI).
    /// </summary>
    private ReadOnlySpan<char> ExtractParameterSpan()
    {
        var remaining = _uri.Slice(_position);
        var end = remaining.IndexOf('/');

        if (end < 0)
        {
            // 到达 URI 末尾
            return remaining;
        }

        if (end == 0)
        {
            // 参数为空
            throw new McpResourceNotFoundException(_context);
        }

        return remaining.Slice(0, end);
    }
}
