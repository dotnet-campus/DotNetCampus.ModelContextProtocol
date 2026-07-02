using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.CompilerServices;

namespace DotNetCampus.ModelContextProtocol.Tests.CompilerServices;

[TestClass]
public class GenerateJsonSchemaTests
{
    [TestMethod("GenerateJsonSchema: 为 JsonTypeInfo 生成 JsonElement Schema 扩展方法")]
    public void GetCompilerGeneratedJsonSchemaReturnsJsonElementSchema()
    {
        var schema = GenerateJsonSchemaTestJsonContext.Default.GenerateJsonSchemaTestModel
            .GetCompilerGeneratedJsonSchema();

        Assert.AreEqual(JsonValueKind.Object, schema.ValueKind);
        Assert.AreEqual("object", schema.GetProperty("type").GetString());

        var properties = schema.GetProperty("properties");
        Assert.AreEqual("string", properties.GetProperty("name").GetProperty("type").GetString());

        var ageType = properties.GetProperty("age").GetProperty("type");
        CollectionAssert.AreEqual(new[] { "integer", "null" }, ReadStringArray(ageType));

        var items = properties.GetProperty("items");
        Assert.AreEqual("array", items.GetProperty("type").GetString());
        Assert.AreEqual("object", items.GetProperty("items").GetProperty("type").GetString());
        Assert.IsTrue(items.GetProperty("items").GetProperty("properties").TryGetProperty("display_name", out _));

        var status = properties.GetProperty("status");
        Assert.AreEqual("string", status.GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "Pending", "Done" }, ReadStringArray(status.GetProperty("enum")));

        CollectionAssert.AreEquivalent(new[] { "name", "items" }, ReadStringArray(schema.GetProperty("required")));
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToArray();
    }
}

[GenerateJsonSchema]
[JsonSerializable(typeof(GenerateJsonSchemaTestModel))]
internal partial class GenerateJsonSchemaTestJsonContext : JsonSerializerContext;

/// <summary>
/// Test Description for GenerateJsonSchemaTestModel
/// </summary>
internal sealed record GenerateJsonSchemaTestModel
{
    /// <summary>
    /// The name for this model which is required.
    /// </summary>
    public required string Name { get; init; }

    public int? Age { get; init; }

    /// <summary>
    /// Items description.
    /// </summary>
    public required IReadOnlyList<GenerateJsonSchemaNestedModel> Items { get; init; }

    /// <summary>
    /// Status description
    /// </summary>
    public GenerateJsonSchemaTestStatus Status { get; init; }
}

/// <summary>
/// Test Description for GenerateJsonSchemaNestedModel
/// </summary>
internal sealed record GenerateJsonSchemaNestedModel
{
    public required bool Enabled { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }
}

/// <summary>
/// Test status.
/// </summary>
internal enum GenerateJsonSchemaTestStatus
{
    /// <summary>
    /// Sample status 1
    /// </summary>
    Pending,
    
    /// <summary>
    /// Sample status 2
    /// </summary>
    Done,
}
