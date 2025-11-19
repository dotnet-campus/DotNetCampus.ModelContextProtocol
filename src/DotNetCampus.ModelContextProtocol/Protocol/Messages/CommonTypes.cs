namespace DotNetCampus.ModelContextProtocol.Protocol.Messages;

/// <summary>
/// 进度令牌，用于将进度通知与原始请求关联。<br/>
/// A progress token, used to associate progress notifications with the original request.
/// </summary>
public sealed record ProgressToken
{
    /// <summary>
    /// 令牌值，可以是字符串或数字<br/>
    /// Token value, can be either string or number
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// 从字符串隐式转换<br/>
    /// Implicit conversion from string
    /// </summary>
    public static implicit operator ProgressToken(string value) => new() { Value = value };

    /// <summary>
    /// 从数字隐式转换<br/>
    /// Implicit conversion from number
    /// </summary>
    public static implicit operator ProgressToken(int value) => new() { Value = value };

    /// <summary>
    /// 从数字隐式转换<br/>
    /// Implicit conversion from number
    /// </summary>
    public static implicit operator ProgressToken(long value) => new() { Value = value };
}

/// <summary>
/// 用于表示分页游标的不透明令牌。<br/>
/// An opaque token used to represent a cursor for pagination.
/// </summary>
public sealed record Cursor
{
    /// <summary>
    /// 游标值<br/>
    /// Cursor value
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// 从字符串隐式转换<br/>
    /// Implicit conversion from string
    /// </summary>
    public static implicit operator Cursor(string value) => new() { Value = value };

    /// <summary>
    /// 转换为字符串<br/>
    /// Convert to string
    /// </summary>
    public static implicit operator string(Cursor cursor) => cursor.Value;
}
