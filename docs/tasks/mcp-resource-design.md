# MCP 资源设计

> **文档状态**：设计阶段  
> **目标受众**：项目维护者、AI 助手  
> **参考协议版本**：MCP 2025-06-18

## 一、概述

### 1.1 什么是 MCP 资源（Resources）

根据 [MCP 官方规范 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/server/resources)，**资源（Resources）** 是 MCP 服务器向 LLM 提供**上下文数据**的机制。与工具（Tools）不同：

- **工具（Tools）**：由 LLM **主动调用**，执行操作并返回结果
- **资源（Resources）**：由客户端 **列出并读取**，为 LLM 提供背景信息

**典型使用场景**：
- 文件系统访问（项目文件、配置文件）
- 数据库查询结果
- API 响应缓存
- 文档、知识库内容

### 1.2 资源与资源模板

MCP 协议支持两种资源发现方式：

#### 1.2.1 静态资源（Resources）
- 通过 `resources/list` 列出所有可用资源
- 每个资源有固定的 URI、名称、描述
- 适用于数量有限、可枚举的资源

**协议示例**：
```json
// 请求
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "resources/list"
}

// 响应
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "resources": [
      {
        "uri": "file:///project/src/main.rs",
        "name": "main.rs",
        "description": "Primary application entry point",
        "mimeType": "text/x-rust"
      }
    ]
  }
}
```

#### 1.2.2 资源模板（Resource Templates）
- 通过 `resources/templates/list` 列出模板
- 使用 **URI 模板**（如 `file:///{path}`）描述参数化资源
- 适用于动态生成、数量巨大的资源（如整个文件系统）

**协议示例**：
```json
// 请求
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "resources/templates/list"
}

// 响应
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "resourceTemplates": [
      {
        "uriTemplate": "file:///{path}",
        "name": "Project Files",
        "description": "Access files in the project directory",
        "mimeType": "application/octet-stream"
      }
    ]
  }
}
```

#### 1.2.3 读取资源

无论静态资源还是模板，都通过 `resources/read` 读取内容：

```json
// 请求
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "resources/read",
  "params": {
    "uri": "file:///project/src/main.rs"
  }
}

// 响应
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "contents": [
      {
        "uri": "file:///project/src/main.rs",
        "mimeType": "text/x-rust",
        "text": "fn main() {\n    println!(\"Hello world!\");\n}"
      }
    ]
  }
}
```

---

## 二、用户侧 API 设计

### 2.1 静态资源声明

```csharp
public class FileResources
{
    /// <summary>
    /// 项目根目录的 README 文件
    /// </summary>
    [McpServerResource(
        Uri = "file:///project/README.md",
        MimeType = "text/markdown")]
    public string GetReadme()
    {
        return File.ReadAllText("README.md");
    }

    /// <summary>
    /// 项目配置文件
    /// </summary>
    [McpServerResource(
        Uri = "file:///project/config.json",
        MimeType = "application/json")]
    public async Task<string> GetConfigAsync(CancellationToken ct)
    {
        return await File.ReadAllTextAsync("config.json", ct);
    }

    /// <summary>
    /// 二进制资源示例
    /// </summary>
    [McpServerResource(
        Uri = "file:///project/logo.png",
        MimeType = "image/png")]
    public byte[] GetLogo()
    {
        return File.ReadAllBytes("logo.png");
    }
}
```

**注册方式**：
```csharp
var server = new McpServerBuilder("my-server", "1.0.0")
    .WithHttp(5000, "mcp")
    .WithResource(() => new FileResources())
    .Build();
```

### 2.2 资源模板声明

```csharp
public class DynamicFileResources
{
    /// <summary>
    /// 访问项目中的任意文件
    /// </summary>
    [McpServerResourceTemplate(
        UriTemplate = "file:///{path}",
        MimeType = "application/octet-stream")]
    public string? ReadFile(string path, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(projectRoot, path);
        if (!File.Exists(fullPath))
            return null;
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// 数据库查询资源
    /// </summary>
    [McpServerResourceTemplate(
        UriTemplate = "db://users/{userId}",
        MimeType = "application/json")]
    public async Task<UserData?> GetUser(int userId, CancellationToken ct)
    {
        return await _db.Users.FindAsync(userId, ct);
    }
}
```

**URI 参数提取规则**：
- 模板中的 `{path}` 自动映射到方法参数 `string path`
- 模板中的 `{userId}` 自动映射到方法参数 `int userId`
- 参数名匹配（大小写不敏感）

### 2.3 返回值类型

| 返回类型 | 生成的 ResourceContents 类型 | 说明 |
|---------|------------------------------|------|
| `string` | `TextResourceContents` | 文本内容，直接设置 `text` 字段 |
| `byte[]` | `BlobResourceContents` | 二进制内容，Base64 编码后设置 `blob` 字段 |
| 可序列化对象 `T` | `TextResourceContents` | JSON 序列化后设置 `text` 字段，MimeType 为 `application/json` |
| `ResourceContents` | 直接返回 | 完全控制 |
| `null` | *(抛出异常)* | 资源不存在 |
| `Task<T>` / `ValueTask<T>` | 同步版本 | 支持异步 |

---

## 三、源生成器架构

### 3.1 生成器结构

参考工具机制，资源机制需要以下源生成器：

1. **`McpServerResourceGenerator`** - 扫描 `[McpServerResource]` 和 `[McpServerResourceTemplate]` 两种特性标记的方法，在内部分别处理生成对应的桥接类
2. **资源拦截器** - 拦截 `WithResource<T>()` 调用（可复用 `InterceptorGenerator`）

### 3.2 生成的桥接类接口

```csharp
namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// 表示一个静态 MCP 资源的接口。
/// </summary>
public interface IMcpServerResource
{
    string Uri { get; }
    Resource GetResourceDefinition();
    ValueTask<ResourceContents> ReadResource(CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示一个资源模板的接口。
/// </summary>
public interface IMcpServerResourceTemplate
{
    string UriTemplate { get; }
    ResourceTemplate GetResourceTemplateDefinition();
    bool TryExtractParameters(string uri, out Dictionary<string, string> parameters);
    ValueTask<ResourceContents?> ReadResource(
        Dictionary<string, string> parameters, 
        CancellationToken cancellationToken = default);
}
```

### 3.3 期望生成的静态资源桥接类

```csharp
#nullable enable
using global::System.Threading.Tasks;
using global::DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace MyNamespace;

public sealed class FileResources_GetReadme_ResourceBridge(
    global::System.Func<FileResources> targetFactory) 
    : global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerResource
{
    private readonly global::System.Func<FileResources> _targetFactory = targetFactory;
    private FileResources Target => _targetFactory();

    public string Uri { get; } = "file:///project/README.md";

    public Resource GetResourceDefinition() => new()
    {
        Uri = "file:///project/README.md",
        Name = "README.md",
        Description = "项目根目录的 README 文件",
        MimeType = "text/markdown",
    };

    public async ValueTask<ResourceContents> ReadResource(
        CancellationToken cancellationToken = default)
    {
        var content = Target.GetReadme();
        return new TextResourceContents
        {
            Uri = "file:///project/README.md",
            MimeType = "text/markdown",
            Text = content,
        };
    }
}
```

### 3.4 期望生成的资源模板桥接类

```csharp
#nullable enable
using global::System.Collections.Generic;
using global::System.Threading;
using global::System.Threading.Tasks;
using global::DotNetCampus.ModelContextProtocol.Protocol.Messages;

namespace MyNamespace;

public sealed class DynamicFileResources_ReadFile_TemplateBridge(
    global::System.Func<DynamicFileResources> targetFactory)
    : global::DotNetCampus.ModelContextProtocol.Servers.IMcpServerResourceTemplate
{
    private readonly global::System.Func<DynamicFileResources> _targetFactory = targetFactory;
    private DynamicFileResources Target => _targetFactory();

    public string UriTemplate { get; } = "file:///{path}";

    public ResourceTemplate GetResourceTemplateDefinition() => new()
    {
        UriTemplate = "file:///{path}",
        Name = "Project Files",
        Description = "访问项目中的任意文件",
        MimeType = "application/octet-stream",
    };

    public bool TryExtractParameters(string uri, out Dictionary<string, string> parameters)
    {
        // URI 模板匹配逻辑
        parameters = new Dictionary<string, string>();
        if (!uri.StartsWith("file:///"))
            return false;
        var path = uri.Substring("file:///".Length);
        parameters["path"] = path;
        return true;
    }

    public async ValueTask<ResourceContents?> ReadResource(
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        var path = parameters["path"];
        var content = Target.ReadFile(path, cancellationToken);
        if (content is null)
            return null;
        return new TextResourceContents
        {
            Uri = $"file:///{path}",
            MimeType = "application/octet-stream",
            Text = content,
        };
    }
}
```

---

## 四、协议层集成

### 4.1 `McpServerResourcesProvider`

```csharp
namespace DotNetCampus.ModelContextProtocol.Servers;

public interface IMcpServerResourcesProvider
{
    IReadOnlyList<IMcpServerResource> GetResources();
    IReadOnlyList<IMcpServerResourceTemplate> GetResourceTemplates();
    ValueTask<ResourceContents?> ReadResourceAsync(string uri, CancellationToken ct);
}

internal sealed class McpServerResourcesProvider : IMcpServerResourcesProvider
{
    private readonly List<IMcpServerResource> _resources = new();
    private readonly List<IMcpServerResourceTemplate> _templates = new();

    public void Add(IMcpServerResource resource) => _resources.Add(resource);
    public void Add(IMcpServerResourceTemplate template) => _templates.Add(template);

    public IReadOnlyList<IMcpServerResource> GetResources() => _resources;
    public IReadOnlyList<IMcpServerResourceTemplate> GetResourceTemplates() => _templates;

    public async ValueTask<ResourceContents?> ReadResourceAsync(string uri, CancellationToken ct)
    {
        // 1. 尝试静态资源
        var staticResource = _resources.FirstOrDefault(r => r.Uri == uri);
        if (staticResource is not null)
            return await staticResource.ReadResource(ct);

        // 2. 尝试资源模板
        foreach (var template in _templates)
        {
            if (template.TryExtractParameters(uri, out var parameters))
            {
                return await template.ReadResource(parameters, ct);
            }
        }

        return null;
    }
}
```

### 4.2 请求处理

在 `McpServer` 中添加资源请求处理：

```csharp
case "resources/list":
{
    var resources = _resourcesProvider.GetResources()
        .Select(r => r.GetResourceDefinition())
        .ToList();
    return new ListResourcesResult { Resources = resources };
}

case "resources/templates/list":
{
    var templates = _resourcesProvider.GetResourceTemplates()
        .Select(t => t.GetResourceTemplateDefinition())
        .ToList();
    return new ListResourceTemplatesResult { ResourceTemplates = templates };
}

case "resources/read":
{
    var uri = request.Params.GetProperty("uri").GetString()!;
    var contents = await _resourcesProvider.ReadResourceAsync(uri, ct);
    if (contents is null)
        throw new McpException($"Resource not found: {uri}");
    return new ReadResourceResult { Contents = new[] { contents } };
}
```

---

## 五、实现任务清单

### 任务 1：协议层基础支持
- [ ] 确保 `Protocol/Messages/` 中已有所有资源相关消息类型
- [ ] 更新 `RequestMethods.cs` 添加资源相关方法常量

### 任务 2：定义用户侧 API
- [ ] 实现 `McpServerResourceAttribute`
- [ ] 实现 `McpServerResourceTemplateAttribute`

### 任务 3：定义桥接类接口
- [ ] 实现 `IMcpServerResource` 接口
- [ ] 实现 `IMcpServerResourceTemplate` 接口

### 任务 4：实现源生成器
- [ ] 创建 `McpServerResourceGeneratingModel`（静态资源模型）
- [ ] 创建 `McpServerResourceTemplateGeneratingModel`（资源模板模型）
- [ ] 实现 `McpServerResourceGenerator`（同时处理两种特性标记，内部分别生成不同桥接类）
- [ ] 实现 `McpServerResourceSourceBuilder`（提供两种桥接类的生成方法）

### 任务 5：实现拦截器
- [ ] 扩展 `InterceptorGenerator` 支持 `WithResource<T>()` 拦截

### 任务 6：实现协议层处理
- [ ] 实现 `McpServerResourcesProvider`
- [ ] 在 `McpServer` 中处理 `resources/list`
- [ ] 在 `McpServer` 中处理 `resources/templates/list`
- [ ] 在 `McpServer` 中处理 `resources/read`

### 任务 7：测试与示例
- [ ] 在 `DotNetCampus.SampleMcpServer` 中添加资源示例
- [ ] 验证生成的代码正确性

---

## 六、技术细节

### 6.1 URI 模板匹配

需要实现一个简单的 URI 模板匹配器，将模板中的参数提取出来。

### 6.2 MIME 类型推断

当用户未指定 `MimeType` 时：
- `string` 返回 → `text/plain`
- `byte[]` 返回 → `application/octet-stream`
- 可序列化对象 → `application/json`

### 6.3 错误处理

- 资源不存在：返回 `null` 并由协议层抛出异常
- URI 格式错误：抛出 `McpException`

---

## 七、参考资源

- [MCP 官方规范 - Resources (2025-06-18)](https://modelcontextprotocol.io/specification/2025-06-18/server/resources)
- [MCP Schema - Resource 相关类型定义](https://github.com/modelcontextprotocol/modelcontextprotocol/blob/main/schema/2025-06-18/schema.ts)
- [本项目的工具设计文档](../knowledge/mcp-tool-design.md)
