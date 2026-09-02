namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 集合返回值的分类。
/// </summary>
public enum CollectionReturnKind
{
    /// <summary>
    /// 不是集合类型。
    /// </summary>
    None,

    /// <summary>
    /// 元素为 string/基本类型/枚举的集合（每个元素 ToString()）。
    /// </summary>
    BasicTypeCollection,

    /// <summary>
    /// 元素为对象的集合（每个元素 JSON 序列化，需 Structured=false）。
    /// </summary>
    ObjectCollection,
}
