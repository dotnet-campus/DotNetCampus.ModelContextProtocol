# Compile-Time JSON Schema Generation

This library provides the `[GenerateJsonSchema]` attribute, which generates JSON Schema at **compile time** — capturing information only available during compilation (such as XML documentation comments) while requiring no runtime reflection.

MCP [Tools](Tools.md) use the exact same Schema generation logic — the generated JSON Schema is identical.

## Basic Usage

### Step 1: Annotate JsonSerializerContext

Add `[GenerateJsonSchema]` to your `JsonSerializerContext`-derived class:

```csharp
[GenerateJsonSchema]
[JsonSerializable(typeof(MyModel))]
[JsonSerializable(typeof(MyOtherModel))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
internal partial class MyJsonContext : JsonSerializerContext;
```

### Step 2: Call the Extension Method

At compile time, each type annotated with `[JsonSerializable]` automatically gets a `GetCompilerGeneratedJsonSchema()` extension method:

```csharp
// Call directly through JsonTypeInfo
JsonElement schema = MyJsonContext.Default.MyModel.GetCompilerGeneratedJsonSchema();

// The result is a standard JSON Schema object
Console.WriteLine(schema.GetProperty("type").GetString());   // "object"
Console.WriteLine(schema.GetProperty("properties"));         // Per-property schemas
Console.WriteLine(schema.GetProperty("required"));           // Required property list
```

## Generated Schema Details

The following model demonstrates all the capabilities of this feature — required, nullable, enum, nested types, arrays, `[JsonPropertyName]`, etc.:

```csharp
/// <summary>
/// Order information
/// </summary>
public record Order
{
    /// <summary>
    /// Order ID
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Order amount (optional)
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// Order status
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// Order items
    /// </summary>
    public required List<OrderItem> Items { get; init; }
}

/// <summary>
/// Order status
/// </summary>
public enum OrderStatus
{
    /// <summary>Pending</summary>
    Pending,
    /// <summary>Shipped</summary>
    Shipped,
    /// <summary>Done</summary>
    Done,
}

/// <summary>
/// Order item
/// </summary>
public record OrderItem
{
    /// <summary>
    /// Product name
    /// </summary>
    [JsonPropertyName("product_name")]
    public required string ProductName { get; init; }

    /// <summary>
    /// Quantity
    /// </summary>
    public int Quantity { get; init; }
}
```

```csharp
var schema = MyJsonContext.Default.Order.GetCompilerGeneratedJsonSchema();
```

Generated JSON Schema:

```json
{
  "type": "object",
  "description": "Order information",
  "properties": {
    "id": { "type": "string", "description": "Order ID" },
    "amount": { "type": ["number", "null"], "description": "Order amount (optional)" },
    "status": {
      "type": "string",
      "description": "Order status",
      "enum": ["Pending", "Shipped", "Done"]
    },
    "items": {
      "type": "array",
      "description": "Order items",
      "items": {
        "type": "object",
        "description": "Order item",
        "properties": {
          "product_name": { "type": "string", "description": "Product name" },
          "quantity": { "type": "integer", "description": "Quantity" }
        },
        "required": ["product_name", "quantity"]
      }
    }
  },
  "required": ["id", "items"]
}
```

Property-by-property breakdown:

| Property       | Mechanism                                                                       |
| -------------- | ------------------------------------------------------------------------------- |
| `id`           | `required` modifier → added to `required` list; XML comment → `description`     |
| `amount`       | `decimal?` nullable value type → `type` becomes `["number", "null"]`            |
| `status`       | Enum type → `enum` array generated; `<summary>` collected automatically         |
| `items`        | Nested type → `items` recursively generates `OrderItem`'s full Schema           |
| `product_name` | `[JsonPropertyName("product_name")]` → Schema property name uses `product_name` |
| `quantity`     | Non-nullable value type → automatically added to `required` list                |

> **Recursive nature**: XML comment extraction is recursive — `<summary>` at any depth of nesting is extracted. Additionally, if a type comes from a base library, NuGet package, or external DLL, as long as its XML documentation is available at compile time, the comments will be included in the Schema.

## Typical Scenario: On-Demand Schema to Avoid Context Pollution

When you need to support dozens of different object types, directly using [polymorphic types](Polymorphism.md) would cause the MCP tool's Schema to include all subtype definitions, significantly bloating it and polluting the agent's context window.

With this feature, you can set the parameter type to `JsonElement` and provide specific schemas on demand through an additional tool.

### The Problem: Schema Bloat from Polymorphism

For example, the following MCP tool uses `EventBase`, which has dozens of subtypes. The MCP tool collects all subtypes into a single JSON Schema, causing severe bloating.

```csharp
[McpServerTool]
public string HandleEvent(EventBase evt) { ... }
```

### The Solution: JsonElement + On-Demand Schema Tool

Step 1: Change the parameter type to `JsonElement`, bypassing subtype schema generation:

```csharp
// Only expose a generic JsonElement, avoiding polymorphism bloat
[McpServerTool]
public string HandleEvent(JsonElement evt) { ... }
```

Step 2: Use `[GenerateJsonSchema]` to generate schemas for each concrete event type:

```csharp
[GenerateJsonSchema]
[JsonSerializable(typeof(ClickEvent))]
[JsonSerializable(typeof(KeyPressEvent))]
[JsonSerializable(typeof(ScrollEvent))]
// ... more event types
internal partial class EventSchemaContext : JsonSerializerContext;
```

Step 3: Add an MCP tool that lets the agent query individual type schemas on demand:

```csharp
/// <summary>
/// Gets the JSON Schema for a specific event type.
/// Call this tool to learn the field structure before constructing an event.
/// </summary>
/// <param name="eventType">Event type name, e.g. "ClickEvent", "KeyPressEvent"</param>
[McpServerTool(ReadOnly = true)]
public JsonElement GetEventSchema(string eventType)
{
    return eventType switch
    {
        nameof(ClickEvent) => EventSchemaContext.Default.ClickEvent
            .GetCompilerGeneratedJsonSchema(),
        nameof(KeyPressEvent) => EventSchemaContext.Default.KeyPressEvent
            .GetCompilerGeneratedJsonSchema(),
        nameof(ScrollEvent) => EventSchemaContext.Default.ScrollEvent
            .GetCompilerGeneratedJsonSchema(),
        _ => throw new ArgumentException($"Unknown event type: {eventType}"),
    };
}
```

### Result

- `HandleEvent`'s Schema stays clean and minimal, exposing only a `JsonElement` parameter
- The agent calls `GetEventSchema` on demand when it needs to understand a specific event's structure
- Both tools share the **exact same schema generation logic**, so `GetEventSchema` returns schemas that precisely match what `HandleEvent` expects
