using System.Text;

namespace DM.Infrastructure.Core.Tests;

internal static class BbTestText
{
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
