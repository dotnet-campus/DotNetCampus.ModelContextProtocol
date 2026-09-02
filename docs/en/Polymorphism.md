# Polymorphic Types

When a tool parameter or return value needs to support multiple subtypes, such as different shapes of messages, events, or commands, you can use C# polymorphic types. This library supports type polymorphism through System.Text.Json's `[JsonPolymorphic]` / `[JsonDerivedType]` attributes.

Polymorphism applies to both tool input parameters and return values.

## Defining Polymorphic Types

An example using type polymorphism:

```csharp
[JsonDerivedType(typeof(PolymorphicDerivedA), "a")]
[JsonDerivedType(typeof(PolymorphicDerivedB), "b")]
[JsonDerivedType(typeof(PolymorphicDerivedC), "c")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
public abstract record PolymorphicBase
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
```

In this example:

- `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` specifies the discriminator JSON property name.
- `[JsonDerivedType(typeof(...), "...")]` declares each subtype and its discriminator value.
- Ensure `JsonSerializerContext` can provide serialization metadata for the polymorphic type; when it is used directly as a tool parameter or return value, register the polymorphic base type.

## Input Polymorphism

A tool method parameter can be declared directly as the polymorphic base type, for example:

```csharp
/// <summary>
/// Tests a polymorphic parameter.
/// </summary>
/// <param name="param">The polymorphic parameter.</param>
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
```

> **⚠ Important**: If the JSON sent by the client omits the discriminator property, or the discriminator value does not match any subtype declared by `[JsonDerivedType]`, this library throws `McpToolMissingRequiredTypeDiscriminatorException` and reports an error to the client. The error message includes the expected discriminator property name and the list of valid values.

Polymorphism is also supported on any property of input parameters, including recursively nested descendant properties.

## Output Polymorphism

A method return value can also be a polymorphic type.

```csharp
/// <summary>
/// Returns a polymorphic object.
/// </summary>
[McpServerTool(ReadOnly = true)]
public PolymorphicBase GetPolymorphicResult()
{
    return new PolymorphicDerivedA { Foo = "result" };
}
```

For non-nullable custom object return values, the library generates polymorphic `outputSchema` and `structuredContent` by default.
