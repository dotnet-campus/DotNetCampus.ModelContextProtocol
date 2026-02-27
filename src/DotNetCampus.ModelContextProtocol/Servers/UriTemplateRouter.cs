using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.ModelContextProtocol.Servers;

/// <summary>
/// URI 模板路由器，用于高效匹配静态 URI 和 RFC 6570 URI 模板。<br/>
/// URI template router for efficient matching of static URIs and RFC 6570 URI templates.
/// </summary>
/// <typeparam name="TResource">资源类型 Resource type</typeparam>
internal sealed class UriTemplateRouter<TResource> where TResource : class
{
    // 静态 URI 的精确匹配（O(1)查找）
    private readonly Dictionary<string, TResource> _exactMatches = new(StringComparer.Ordinal);

    // URI 模板的前缀树节点（支持参数匹配）
    private readonly TrieNode _templateRoot = new();

    /// <summary>
    /// 添加静态 URI 资源（无参数）。<br/>
    /// Add a static URI resource (no parameters).
    /// </summary>
    public bool AddExactUri(string uri, TResource resource)
    {
        return _exactMatches.TryAdd(uri, resource);
    }

    /// <summary>
    /// 添加 URI 模板资源（包含 {param} 参数）。<br/>
    /// Add a URI template resource (with {param} parameters).
    /// </summary>
    public void AddTemplate(string uriTemplate, TResource resource)
    {
        var segments = ParseTemplate(uriTemplate);
        var node = _templateRoot;

        foreach (var segment in segments)
        {
            if (segment.IsParameter)
            {
                // 参数段：使用通配符匹配
                node = node.GetOrAddChild("*", isParameter: true, segment.ParameterName);
            }
            else
            {
                // 静态段：精确匹配
                node = node.GetOrAddChild(segment.Value, isParameter: false);
            }
        }

        node.Resource = resource;
    }

    /// <summary>
    /// 尝试匹配给定的 URI，返回匹配的资源和提取的参数。<br/>
    /// Try to match the given URI and return the matched resource and extracted parameters.
    /// </summary>
    public bool TryMatch(string uri, [NotNullWhen(true)] out TResource? resource, out Dictionary<string, string>? parameters)
    {
        // 1. 优先尝试精确匹配（最快）
        if (_exactMatches.TryGetValue(uri, out resource))
        {
            parameters = null;
            return true;
        }

        // 2. 尝试模板匹配
        var segments = SplitUri(uri);
        parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        if (TryMatchTemplate(_templateRoot, segments, 0, parameters, out resource))
        {
            return true;
        }

        resource = null;
        parameters = null;
        return false;
    }

    /// <summary>
    /// 获取所有已注册资源（用于 ListResources）。<br/>
    /// Get all registered resources (for ListResources).
    /// </summary>
    public IEnumerable<TResource> GetAllResources()
    {
        return _exactMatches.Values;
    }

    /// <summary>
    /// 获取所有已注册的模板资源（用于 ListResourceTemplates）。<br/>
    /// Get all registered template resources (for ListResourceTemplates).
    /// </summary>
    public IEnumerable<TResource> GetAllTemplates()
    {
        var templates = new List<TResource>();
        CollectTemplates(_templateRoot, templates);
        return templates;
    }

    private void CollectTemplates(TrieNode node, List<TResource> templates)
    {
        if (node.Resource is not null)
        {
            templates.Add(node.Resource);
        }

        foreach (var child in node.Children.Values)
        {
            CollectTemplates(child, templates);
        }
    }

    private bool TryMatchTemplate(
        TrieNode node,
        ReadOnlySpan<string> segments,
        int index,
        Dictionary<string, string> parameters,
        [NotNullWhen(true)] out TResource? resource)
    {
        // 到达末尾，检查是否有资源
        if (index >= segments.Length)
        {
            resource = node.Resource;
            return resource is not null;
        }

        var segment = segments[index];

        // 1. 尝试精确匹配
        if (node.Children.TryGetValue(segment, out var exactNode))
        {
            if (TryMatchTemplate(exactNode, segments, index + 1, parameters, out resource))
            {
                return true;
            }
        }

        // 2. 尝试参数匹配
        if (node.Children.TryGetValue("*", out var wildcardNode))
        {
            // 提取参数值
            if (wildcardNode.ParameterName is not null)
            {
                parameters[wildcardNode.ParameterName] = segment;
            }

            if (TryMatchTemplate(wildcardNode, segments, index + 1, parameters, out resource))
            {
                return true;
            }

            // 回溯：移除参数
            if (wildcardNode.ParameterName is not null)
            {
                parameters.Remove(wildcardNode.ParameterName);
            }
        }

        resource = null;
        return false;
    }

    /// <summary>
    /// 解析 URI 模板为段列表。<br/>
    /// Parse URI template into segment list.
    /// </summary>
    private static List<TemplateSegment> ParseTemplate(string uriTemplate)
    {
        var segments = new List<TemplateSegment>();
        var span = uriTemplate.AsSpan();
        var segmentStart = 0;

        // 跳过协议部分（如 "test://"）
        var protocolEnd = span.IndexOf("://");
        if (protocolEnd >= 0)
        {
            segmentStart = protocolEnd + 3;
        }

        for (var i = segmentStart; i < span.Length; i++)
        {
            if (span[i] == '/')
            {
                if (i > segmentStart)
                {
                    var segment = span[segmentStart..i];
                    segments.Add(ParseSegment(segment));
                }
                segmentStart = i + 1;
            }
        }

        // 最后一段
        if (segmentStart < span.Length)
        {
            var segment = span[segmentStart..];
            segments.Add(ParseSegment(segment));
        }

        return segments;
    }

    private static TemplateSegment ParseSegment(ReadOnlySpan<char> segment)
    {
        // 检查是否是参数段 {param}
        if (segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
        {
            var paramName = segment[1..^1].ToString();
            return new TemplateSegment
            {
                IsParameter = true,
                ParameterName = paramName,
                Value = string.Empty,
            };
        }

        return new TemplateSegment
        {
            IsParameter = false,
            Value = segment.ToString(),
        };
    }

    /// <summary>
    /// 分割 URI 为段数组（用于匹配）。<br/>
    /// Split URI into segment array (for matching).
    /// </summary>
    private static string[] SplitUri(string uri)
    {
        var span = uri.AsSpan();
        var segmentStart = 0;

        // 跳过协议部分
        var protocolEnd = span.IndexOf("://");
        if (protocolEnd >= 0)
        {
            segmentStart = protocolEnd + 3;
        }

        var segments = new List<string>();
        for (var i = segmentStart; i < span.Length; i++)
        {
            if (span[i] == '/')
            {
                if (i > segmentStart)
                {
                    segments.Add(span[segmentStart..i].ToString());
                }
                segmentStart = i + 1;
            }
        }

        // 最后一段
        if (segmentStart < span.Length)
        {
            segments.Add(span[segmentStart..].ToString());
        }

        return segments.ToArray();
    }

    private struct TemplateSegment
    {
        public bool IsParameter;
        public string Value;
        public string? ParameterName;
    }

    private sealed class TrieNode
    {
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.Ordinal);
        public TResource? Resource { get; set; }
        public bool IsParameter { get; set; }
        public string? ParameterName { get; set; }

        public TrieNode GetOrAddChild(string key, bool isParameter, string? parameterName = null)
        {
            if (!Children.TryGetValue(key, out var child))
            {
                child = new TrieNode
                {
                    IsParameter = isParameter,
                    ParameterName = parameterName,
                };
                Children[key] = child;
            }
            return child;
        }
    }
}
