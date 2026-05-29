using DotNetCampus.ModelContextProtocol.Properties;
using Microsoft.CodeAnalysis;
using static DotNetCampus.ModelContextProtocol.Properties.Localizations;

// ReSharper disable InconsistentNaming

namespace DotNetCampus.ModelContextProtocol;

/// <summary>
/// 包含日志库中的所有诊断。
/// </summary>
public class Diagnostics
{
    public static DiagnosticDescriptor DM0000_UnknownError { get; } = new(
        nameof(DM0000),
        Localize(nameof(DM0000)),
        Localize(nameof(DM0000_Message)),
        Categories.Compiler,
        DiagnosticSeverity.Error,
        true);

    public static DiagnosticDescriptor DM0101_McpToolCollectionReturnTypeNotSupported { get; } = new(
        nameof(DM0101),
        Localize(nameof(DM0101)),
        Localize(nameof(DM0101_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0101_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0102_McpToolVoidReturnType { get; } = new(
        nameof(DM0102),
        Localize(nameof(DM0102)),
        Localize(nameof(DM0102_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0102_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0103_McpToolPrimitiveReturnType { get; } = new(
        nameof(DM0103),
        Localize(nameof(DM0103)),
        Localize(nameof(DM0103_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0103_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0104_McpToolEnumReturnType { get; } = new(
        nameof(DM0104),
        Localize(nameof(DM0104)),
        Localize(nameof(DM0104_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0104_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0105_McpToolStructuredNotAllowed { get; } = new(
        nameof(DM0105),
        Localize(nameof(DM0105)),
        Localize(nameof(DM0105_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0105_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0106_McpToolNullableRequiresStructured { get; } = new(
        nameof(DM0106),
        Localize(nameof(DM0106)),
        Localize(nameof(DM0106_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0106_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    public static DiagnosticDescriptor DM0107_McpToolNullableStructuredTrue { get; } = new(
        nameof(DM0107),
        Localize(nameof(DM0107)),
        Localize(nameof(DM0107_Message)),
        Categories.RuntimeException,
        DiagnosticSeverity.Error,
        true,
        description: Localize(nameof(DM0107_Description)),
        customTags: WellKnownDiagnosticTags.NotConfigurable);

    private static class Categories
    {
        /// <summary>
        /// 可能产生 bug，则报告此诊断。
        /// </summary>
        public const string AvoidBugs = "DotNetCampus.AvoidBugs";

        /// <summary>
        /// 为了提供代码生成能力，则报告此诊断。
        /// </summary>
        public const string CodeFixOnly = "DotNetCampus.CodeFixOnly";

        /// <summary>
        /// 因编译要求而必须满足的条件没有满足，则报告此诊断。
        /// </summary>
        public const string Compiler = "DotNetCampus.Compiler";

        /// <summary>
        /// 因库内的机制限制，必须满足此要求后库才可正常工作，则报告此诊断。
        /// </summary>
        public const string Mechanism = "DotNetCampus.Mechanism";

        /// <summary>
        /// 为了代码可读性，使之更易于理解、方便调试，则报告此诊断。
        /// </summary>
        public const string Readable = "DotNetCampus.Readable";

        /// <summary>
        /// 为了提升性能，或避免性能问题，则报告此诊断。
        /// </summary>
        public const string Performance = "DotNetCampus.Performance";

        /// <summary>
        /// 能写得出来正常编译，但会引发运行时异常，则报告此诊断。
        /// </summary>
        public const string RuntimeException = "DotNetCampus.RuntimeException";

        /// <summary>
        /// 编写了无法生效的代码，则报告此诊断。
        /// </summary>
        public const string Useless = "DotNetCampus.Useless";
    }

    private static LocalizableString Localize(string key) => new LocalizableResourceString(key, ResourceManager, typeof(Localizations));
}
