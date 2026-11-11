using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;

namespace DM.Architecture.Tests;

// What only one rule reads stays private to it.
internal static class DeploymentFiles
{
    internal const string BaseCompose = "docker-compose.yml";
    internal const string PreviewCompose = "docker-compose.preview.yml";
    internal const string PopCompose = "docker-compose.pop.yml";
    internal const string NginxConfiguration = "nginx/nginx.conf";
    private const string EdgeLocations = "nginx/edge-locations.conf";

    internal static string DockerDirectory => Path.Combine(RepositoryLayout.Root, "docker");

    internal static string Read(string fileName)
    {
        var path = Path.Combine(DockerDirectory, fileName);
        File.Exists(path).Should().BeTrue($"{fileName} must exist at {path}");
        return File.ReadAllText(path);
    }

    // Both servers of the edge include the same locations, so nginx.conf alone has no routes.
    internal static string EdgeAsAssembled() => Read(NginxConfiguration)
        .Replace("include /etc/nginx/edge-locations.conf;", Read(EdgeLocations), StringComparison.Ordinal);

    /// <summary>The text between two markers, or null when the opening one is absent.</summary>
    internal static string? Between(string text, string opening, string closing)
    {
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += opening.Length;
        var end = text.IndexOf(closing, start, StringComparison.Ordinal);
        return end < 0 ? null : text[start..end];
    }

    internal static string ServiceBlock(string compose, string name)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith($"  {name}:", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, $"the compose file must declare {name}");
        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^  \S")));
    }

    // By container name: a target is written as the hostname another container resolves.
    internal static string ServiceWithContainerName(string compose, string containerName)
    {
        var lines = compose.Split('\n');
        var marker = Array.FindIndex(lines, line =>
            line.TrimStart().StartsWith("container_name:", StringComparison.Ordinal) &&
            line.Contains(containerName, StringComparison.Ordinal));

        marker.Should().BeGreaterThan(-1, $"no service of the stack answers to {containerName}");

        var start = marker;
        while (start > 0 && !Regex.IsMatch(lines[start], @"^  \S"))
        {
            start--;
        }

        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^  \S")));
    }

    internal static string? FindValue(string compose, string key) => Uncommented(compose)
        .Select(line =>
        {
            var marker = line.IndexOf(key, StringComparison.Ordinal);
            return marker < 0 ? null : line[(marker + key.Length)..].Trim().Trim('\'', '"', '-', ' ');
        })
        .FirstOrDefault(value => !string.IsNullOrEmpty(value));

    internal static string ActiveDirectives(string configuration) => string.Join('\n', Uncommented(configuration));

    private static readonly HashSet<string> OptionsTakingAValue = new(StringComparer.Ordinal)
    {
        "--add-host", "--entrypoint", "--env", "--env-file", "--label", "--name",
        "--network", "--platform", "--publish", "--pull", "--user", "--volume",
        "--workdir", "-e", "-l", "-p", "-u", "-v", "-w",
    };

    // Shell is the third place an image reference lives, and dependabot does not read it.
    /// <remarks>
    /// An image held in a variable is read from the script's own assignment, as in
    /// IMAGE="nginx:1.30-alpine" or IMAGE="${IMAGE:-nginx:1.30-alpine}"; one the script computes
    /// with a command substitution stays the variable it is. Anything else fails, since a
    /// skipped image is neither pinned nor named.
    /// </remarks>
    internal static List<(string Script, string Image)> ImagesRunByShellScripts(string directory)
    {
        var found = new List<(string Script, string Image)>();

        foreach (var script in Directory
            .EnumerateFiles(directory, "*.sh", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            // Comments go first, since they quote commands; then continuations are folded. A
            // substitution is collapsed only inside the arguments, so a run it holds is still
            // found. The second alternative is the installer's "${DOCKER[@]}" array.
            var text = File.ReadAllText(script);
            var code = Regex.Replace(text, @"^\s*#.*$", string.Empty, RegexOptions.Multiline);
            var folded = Regex.Replace(code, @"\\\r?\n\s*", " ");

            foreach (Match invocation in Regex.Matches(
                folded, @"(?:docker|DOCKER\[@\]\}""?)\s+run\s+(?<arguments>[^\r\n]*)"))
            {
                // A substitution, which may hold spaces, becomes one word.
                var arguments = Regex.Replace(invocation.Groups["arguments"].Value, @"\$\([^()]*\)", "X");
                var image = ImageOperandOf(arguments)
                    ?? throw new InvalidOperationException(
                        $"{script}: no image in \"{invocation.Value.Trim()}\", so nothing pins or names what it runs");
                found.Add((script, Assigned(script, text, image)));
            }
        }

        return found;
    }

    private static string Assigned(string path, string script, string operand)
    {
        var variable = Regex.Match(operand, @"^\$\{?(?<name>\w+)\}?$");
        if (!variable.Success)
        {
            return operand;
        }

        var name = variable.Groups["name"].Value;
        var assignment = Regex.Match(script,
            $@"^\s*(?:(?:local|export|readonly|declare)\s+)?{name}=(?<value>.*?)\s*$", RegexOptions.Multiline);
        var value = assignment.Groups["value"].Value.Trim('\'', '"');

        if (Regex.Match(value, @"^\$\{\w+:?-(?<default>[^'""\s${}]+)\}$") is { Success: true } fallback)
        {
            return fallback.Groups["default"].Value;
        }

        if (value.StartsWith("$(", StringComparison.Ordinal))
        {
            return operand;
        }

        return assignment.Success && Regex.IsMatch(value, @"^[^'""\s$]+$")
            ? value
            : throw new InvalidOperationException(
                $"{path}: {operand} is assigned neither a literal image nor a command substitution");
    }

    private static string? ImageOperandOf(string arguments)
    {
        // The command ends at a separator; "f() { docker run "$@"; }" hands its image in from outside.
        var tokens = Regex.Split(arguments, @"[;|&]")[0]
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('\'', '"'))
            .Where(token => token.Length > 0)
            .ToList();

        for (var index = 0; index < tokens.Count; index++)
        {
            // "$@" hands a function's arguments on as more options.
            if (tokens[index][0] != '-' && tokens[index] != "$@")
            {
                return tokens[index];
            }

            // Without the skip an "--entrypoint sh" hands back sh as the image. The
            // --option=value spelling is one token and needs none.
            if (OptionsTakingAValue.Contains(tokens[index]))
            {
                index++;
            }
        }

        return null;
    }

    private static IEnumerable<string> Uncommented(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'));
}
