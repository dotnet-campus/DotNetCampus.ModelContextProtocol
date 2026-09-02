using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCampus.ModelContextProtocol.Exceptions;
using DotNetCampus.ModelContextProtocol.Protocol.Messages;
using DotNetCampus.ModelContextProtocol.Tests.McpTools;

namespace DotNetCampus.ModelContextProtocol.Tests.CompilerServices;

[TestClass]
public class ToolSchemaContractTests
{
    [TestMethod("Tool schema: enum values and defaults use STJ wire names")]
    public async Task EnumSchemaUsesWireNames()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "echo_enum");
        var properties = tool.InputSchema.GetProperty("properties");
        var value = properties.GetProperty("value");

        CollectionAssert.AreEqual(new[] { "PlainText", "jsonObject" }, ReadStringArray(value.GetProperty("enum")));
        Assert.AreEqual("jsonObject", value.GetProperty("default").GetString());

        var items = properties.GetProperty("values").GetProperty("items");
        CollectionAssert.AreEqual(new[] { "PlainText", "jsonObject" }, ReadStringArray(items.GetProperty("enum")));
    }

    [TestMethod("Tool call: enum return values use STJ wire names")]
    public async Task EnumReturnUsesWireNames()
    {
        await using var package = await CreatePackageAsync();

        var single = await package.Client.CallToolAsync("return_enum");
        Assert.AreEqual("jsonObject", GetSingleText(single));

        var list = await package.Client.CallToolAsync("return_enum_list");
        CollectionAssert.AreEqual(new[] { "PlainText", "jsonObject" }, list.Content.OfType<TextContentBlock>().Select(x => x.Text).ToArray());
    }

    [TestMethod("Tool schema: object properties follow basic STJ contract attributes")]
    public async Task ObjectSchemaFollowsBasicStjAttributes()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "echo_contract_object");
        var input = tool.InputSchema.GetProperty("properties").GetProperty("input");
        var properties = input.GetProperty("properties");

        Assert.IsTrue(properties.TryGetProperty("requiredNullableName", out var requiredNullableName));
        CollectionAssert.AreEqual(new[] { "string", "null" }, ReadStringArray(requiredNullableName.GetProperty("type")));
        Assert.IsTrue(properties.TryGetProperty("jsonRequiredValue", out _));
        Assert.IsFalse(properties.TryGetProperty("ignoredValue", out _));
        Assert.IsTrue(properties.TryGetProperty("custom_mode", out var mode));
        CollectionAssert.AreEqual(new[] { "PlainText", "jsonObject" }, ReadStringArray(mode.GetProperty("enum")));
        CollectionAssert.AreEquivalent(new[] { "requiredNullableName", "jsonRequiredValue" }, ReadStringArray(input.GetProperty("required")));
    }

    [TestMethod("Tool schema: InputObject uses object contract as top-level schema")]
    public async Task InputObjectSchemaUsesObjectContract()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "echo_contract_input_object");
        var properties = tool.InputSchema.GetProperty("properties");

        Assert.IsTrue(properties.TryGetProperty("requiredNullableName", out _));
        Assert.IsFalse(properties.TryGetProperty("ignoredValue", out _));
        Assert.IsTrue(properties.TryGetProperty("custom_mode", out _));
    }

    [TestMethod("Tool schema: dictionary values are represented by additionalProperties schema")]
    public async Task DictionarySchemaUsesAdditionalPropertiesValueSchema()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "echo_dictionary");
        var values = tool.InputSchema.GetProperty("properties").GetProperty("values");

        Assert.AreEqual("object", values.GetProperty("type").GetString());
        Assert.AreEqual("integer", values.GetProperty("additionalProperties").GetProperty("type").GetString());
    }

    [TestMethod("Tool schema: polymorphic int discriminator keeps numeric const")]
    public async Task PolymorphicIntDiscriminatorKeepsNumericConst()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "echo_polymorphic");
        var value = tool.InputSchema.GetProperty("properties").GetProperty("value");
        var first = value.GetProperty("anyOf").EnumerateArray().First();
        var discriminator = first.GetProperty("properties").GetProperty("kind").GetProperty("const");

        Assert.AreEqual(JsonValueKind.Number, discriminator.ValueKind);
        Assert.AreEqual(1, discriminator.GetInt32());
    }

    [TestMethod("Tool schema: structured output follows object contract")]
    public async Task StructuredOutputSchemaFollowsObjectContract()
    {
        await using var package = await CreatePackageAsync();

        var tool = await GetToolAsync(package, "return_contract_output");
        var outputSchema = tool.OutputSchema!.Value;
        var properties = outputSchema.GetProperty("properties");

        Assert.IsTrue(properties.TryGetProperty("requiredNullableName", out _));
        Assert.IsFalse(properties.TryGetProperty("ignoredValue", out _));
        Assert.IsTrue(properties.TryGetProperty("mode", out var mode));
        CollectionAssert.AreEqual(new[] { "PlainText", "jsonObject" }, ReadStringArray(mode.GetProperty("enum")));
    }

    [TestMethod("Tool schema: missing JsonTypeInfo throws")]
    public async Task ToolSchemaThrowsWhenJsonTypeInfoMissing()
    {
        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(builder => builder
            .WithJsonSerializer(MissingSchemaContractJsonContext.Default)
            .WithTools(t => t.WithTool(() => new SchemaContractTool())));

        var tool = package.Server.Tools.Single(x => x.ToolName == "echo_contract_object");

        Assert.ThrowsException<McpToolJsonTypeInfoNotFoundException>(() =>
            tool.GetToolDefinition(MissingSchemaContractJsonContext.Default));
    }

    [TestMethod("Tool schema: InputObject object properties use runtime JsonTypeInfo naming policy")]
    public async Task InputObjectSchemaUsesRuntimeJsonTypeInfoNamingPolicy()
    {
        await using var package = await TestMcpFactory.Shared.CreateInProcessCoreAsync(builder => builder
            .WithJsonSerializer(SnakeCaseSchemaContractJsonContext.Default)
            .WithTools(t => t.WithTool(() => new SchemaContractTool())));

        var tool = await GetToolAsync(package, "echo_contract_input_object");
        var properties = tool.InputSchema.GetProperty("properties");

        Assert.IsTrue(properties.TryGetProperty("required_nullable_name", out _));
        Assert.IsTrue(properties.TryGetProperty("json_required_value", out _));
        Assert.IsTrue(properties.TryGetProperty("custom_mode", out _));
        CollectionAssert.AreEquivalent(new[] { "required_nullable_name", "json_required_value" }, ReadStringArray(tool.InputSchema.GetProperty("required")));
    }

    private static async Task<McpTestingPackage> CreatePackageAsync()
    {
        return await TestMcpFactory.Shared.CreateInProcessCoreAsync(builder => builder
            .WithJsonSerializer(TestToolJsonContext.Default)
            .WithTools(t => t.WithTool(() => new SchemaContractTool())));
    }

    private static async Task<Tool> GetToolAsync(McpTestingPackage package, string name)
    {
        var result = await package.Client.ListToolsAsync();
        return result.Tools.Single(x => x.Name == name);
    }

    private static string GetSingleText(CallToolResult result)
    {
        var content = result.Content.Single();
        Assert.IsInstanceOfType<TextContentBlock>(content);
        return ((TextContentBlock)content).Text;
    }

    private static string[] ReadStringArray(JsonElement element)
    {
        return element.EnumerateArray()
            .Select(x => x.GetString()!)
            .ToArray();
    }
}

[JsonSerializable(typeof(SchemaContractEnum))]
[JsonSerializable(typeof(IReadOnlyList<SchemaContractEnum>))]
[JsonSerializable(typeof(SchemaContractInput))]
[JsonSerializable(typeof(SchemaContractOutput))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(SchemaContractPolymorphicBase))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class SnakeCaseSchemaContractJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(SchemaContractEnum))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    UseStringEnumConverter = true,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class MissingSchemaContractJsonContext : JsonSerializerContext;
