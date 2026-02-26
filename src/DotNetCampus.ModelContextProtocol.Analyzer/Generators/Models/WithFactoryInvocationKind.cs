namespace DotNetCampus.ModelContextProtocol.Generators.Models;

/// <summary>
/// 调用是否由工厂委托创建实例。
/// </summary>
public enum WithFactoryInvocationKind
{
    /// <summary>
    /// 使用工厂委托重载（Func&lt;T&gt;）。
    /// </summary>
    WithFactory,

    /// <summary>
    /// 不使用工厂委托重载。
    /// </summary>
    WithoutFactory,
}
