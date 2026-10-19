using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;

namespace DM.Architecture.Tests;

/// <summary>
/// The files of the deployment, and the readers that two or more of its rules share.
/// </summary>
/// <remarks>What only one rule reads stays private to it.</remarks>
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

    /// <summary>
    /// The edge as nginx assembles it: the file with its include resolved.
    /// </summary>
    /// <remarks>Both servers of the edge include the same locations, so nginx.conf alone has no routes.</remarks>
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

    /// <summary>The body of one service, up to the next key at the same indent.</summary>
    internal static string ServiceBlock(string compose, string name)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith($"  {name}:", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, $"the compose file must declare {name}");
        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^  \S")));
    }

    /// <summary>The body of the service a container name belongs to.</summary>
    /// <remarks>By container name: a target is written as the hostname another container resolves.</remarks>
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

    /// <summary>Directives that are not commented out.</summary>
    internal static string ActiveDirectives(string configuration) => string.Join('\n', Uncommented(configuration));

    private static IEnumerable<string> Uncommented(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'));
}
