using System.Text;

namespace DotNetCampus.ModelContextProtocol.Utils;

internal static class NamingHelper
{
    /// <summary>
    /// 从其他命名法转换为 kebab-case 命名法。
    /// </summary>
    /// <param name="oldName">其他命名法的名称。</param>
    /// <param name="isUpperSeparator">大写字母是否是单词分隔符。例如 SampleName_Text -> sample-name-text | samplename-text。</param>
    /// <param name="toLower">是否将所有字母转换为小写形式。例如 Sample-Name -> sample-name | Sample-Name。</param>
    /// <returns>kebab-case 命名法的字符串。</returns>
    internal static string MakeKebabCase(string oldName, bool isUpperSeparator = true, bool toLower = true)
        => ConvertToDelimitedCase(oldName, '-', isUpperSeparator, toLower);

    /// <summary>
    /// 从其他命名法转换为 snake_case 命名法。
    /// </summary>
    /// <param name="oldName">其他命名法的名称。</param>
    /// <param name="isUpperSeparator">大写字母是否是单词分隔符。例如 SampleName_Text -> sample_name_text | samplename_text。</param>
    /// <param name="toLower">是否将所有字母转换为小写形式。例如 Sample_Name -> sample_name | Sample_Name。</param>
    /// <returns>snake_case 命名法的字符串。</returns>
    internal static string MakeSnakeCase(string oldName, bool isUpperSeparator = true, bool toLower = true)
        => ConvertToDelimitedCase(oldName, '_', isUpperSeparator, toLower);

    /// <summary>
    /// 从其他命名法转换为指定分隔符的命名法。
    /// </summary>
    /// <param name="oldName">其他命名法的名称。</param>
    /// <param name="delimiter">单词分隔符（如 '-' 表示 kebab-case，'_' 表示 snake_case）。</param>
    /// <param name="isUpperSeparator">大写字母是否是单词分隔符。例如 SampleName_Text -> Sample-Name-Text | SampleName-Text。</param>
    /// <param name="toLower">是否将所有字母转换为小写形式。例如 Sample-Name -> sample-name | Sample-Name。</param>
    /// <returns>转换后的字符串。</returns>
    private static string ConvertToDelimitedCase(string oldName, char delimiter, bool isUpperSeparator, bool toLower)
    {
        var builder = new StringBuilder();

        var isFirstLetter = true;
        var isUpperLetter = false;
        var isSeparator = false;
        for (var i = 0; i < oldName.Length; i++)
        {
            var c = oldName[i];
            if (!char.IsLetterOrDigit(c))
            {
                isUpperLetter = false;
                // Append nothing because delimited case has no continuous special characters.
                if (!isFirstLetter)
                {
                    isSeparator = true;
                }
                continue;
            }

            if (isFirstLetter)
            {
                if (char.IsDigit(c))
                {
                    // delimited case does not support digital as the first letter.
                    isSeparator = false;
                }
                else if (char.IsUpper(c))
                {
                    // 大写字母。
                    isFirstLetter = false;
                    isUpperLetter = true;
                    isSeparator = false;
                    builder.Append(toLower ? char.ToLowerInvariant(c) : c);
                }
                else if (char.IsLower(c))
                {
                    // 小写字母。
                    isFirstLetter = false;
                    isUpperLetter = false;
                    isSeparator = false;
                    builder.Append(c);
                }
                else
                {
                    isFirstLetter = false;
                    isUpperLetter = false;
                    builder.Append(c);
                }
            }
            else
            {
                if (char.IsDigit(c))
                {
                    isUpperLetter = false;
                    isSeparator = false;
                    builder.Append(c);
                }
                else if (char.IsUpper(c))
                {
                    if (!isUpperLetter && (isUpperSeparator || isSeparator))
                    {
                        builder.Append(delimiter);
                    }
                    isUpperLetter = true;
                    isSeparator = false;
                    builder.Append(toLower ? char.ToLowerInvariant(c) : c);
                }
                else if (char.IsLower(c))
                {
                    if (isSeparator)
                    {
                        builder.Append(delimiter);
                    }
                    isUpperLetter = false;
                    isSeparator = false;
                    builder.Append(c);
                }
                else
                {
                    if (isSeparator)
                    {
                        builder.Append(delimiter);
                    }
                    builder.Append(c);
                }
            }
        }

        return builder.ToString();
    }
}
