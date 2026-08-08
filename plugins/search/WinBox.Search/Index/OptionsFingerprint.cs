using System.Security.Cryptography;
using System.Text;

namespace WinBox.Search.Index;

/// <summary>
/// Stable hash of scan policy fields. Used to detect when a persisted DB must be rebuilt.
/// Does not include <see cref="IndexOptions.IndexStoreDirectory"/> or
/// <see cref="IndexOptions.MaxInMemoryMegabytes"/> (runtime placement ≠ scan content).
/// </summary>
public static class OptionsFingerprint
{
    public static string Compute(IndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sb = new StringBuilder(512);
        AppendList(sb, "roots", options.Roots);
        AppendList(sb, "excludeRoots", options.ExcludeRoots);
        AppendList(sb, "includeExt", options.IncludeExtensions);
        AppendList(sb, "excludeExt", options.ExcludeExtensions);
        AppendList(sb, "includePath", options.IncludePathPatterns);
        AppendList(sb, "excludePath", options.ExcludePathPatterns);
        sb.Append("recursive=").Append(options.Recursive ? '1' : '0');

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static void AppendList(StringBuilder sb, string key, IReadOnlyList<string> values)
    {
        sb.Append(key).Append('=');
        var ordered = values
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Select(static v => v.Trim())
            .OrderBy(static v => v, StringComparer.OrdinalIgnoreCase);
        var first = true;
        foreach (var value in ordered)
        {
            if (!first)
            {
                sb.Append('\u001f');
            }

            first = false;
            sb.Append(value);
        }

        sb.Append('\n');
    }
}
