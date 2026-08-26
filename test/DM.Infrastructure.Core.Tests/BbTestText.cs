using System.Text;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The oversized inputs the parser rules are measured against.
/// </summary>
internal static class BbTestText
{
    /// <summary>One fragment written out the given number of times.</summary>
    internal static string Repeat(string fragment, int times)
    {
        var builder = new StringBuilder(fragment.Length * times);
        for (var index = 0; index < times; index++)
        {
            builder.Append(fragment);
        }

        return builder.ToString();
    }
}
