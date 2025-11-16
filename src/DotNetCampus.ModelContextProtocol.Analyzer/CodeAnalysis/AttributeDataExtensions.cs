using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.CodeAnalysis;

internal static class AttributeDataExtensions
{
    public static T? GetObjectOrDefault<T>(this ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string name)
        where T : class
    {
        return TryGetValue<T>(namedArguments, name, out var value) ? value : null;
    }

    public static T? GetValueOrDefault<T>(this ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string name)
        where T : struct
    {
        return TryGetValue<T>(namedArguments, name, out var value) ? value : null;
    }

    public static bool TryGetValue<T>(this ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments, string name, [NotNullWhen(true)] out T? value)
    {
        var argument = namedArguments.FirstOrDefault(x => x.Key == name);
        if (argument.Value.IsNull)
        {
            value = default;
            return false;
        }

        value = typeof(T) switch
        {
            var t when t == typeof(string) => (T)(object)argument.Value.Value!.ToString(),
            var t when t == typeof(bool) => (T)(object)(bool)argument.Value.Value!,
            { IsEnum: true } => (T)argument.Value.Value!,
            _ => throw new NotSupportedException($"Type {typeof(T)} is not supported"),
        };
        return true;
    }
}
