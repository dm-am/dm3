using System;
using System.IO;
using System.Text.Json.Nodes;

namespace DM.Testing.Markup;

/// <summary>
/// A table the client declares once and the server tests read too: strict JSON between
/// two marker comments of a client source.
/// </summary>
public static class ClientTable
{
    /// <summary>The JSON between "/* name:begin */" and "/* name:end */" of a file.</summary>
    public static JsonNode Read(string file, string name)
    {
        var begin = $"/* {name}:begin */";
        var end = $"/* {name}:end */";
        var text = File.ReadAllText(file);
        var start = text.IndexOf(begin, StringComparison.Ordinal);
        var stop = text.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || stop < start)
        {
            throw new InvalidOperationException($"{file} lost its {begin} and {end} markers");
        }

        return JsonNode.Parse(text[(start + begin.Length)..stop])!;
    }
}
