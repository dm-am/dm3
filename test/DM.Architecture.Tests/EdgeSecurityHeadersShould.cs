using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// An edge that sets a security header sends one copy of it.
/// </summary>
/// <remarks>Read per block: add_header and proxy_hide_header inherit by block.</remarks>
public class EdgeSecurityHeadersShould
{
    /// <summary>
    /// Wherever an edge sets a security header, the copy the upstream sent is
    /// dropped, so the response leaves with one.
    /// </summary>
    /// <remarks>
    /// add_header appends to what the upstream sent, so the edit of one layer would be decided by
    /// header parsing rules. Asked of the running server, the certificate template and the point of
    /// presence.
    /// </remarks>
    [Fact]
    public void SendOneCopyOfEverySecurityHeaderTheEdgeSets()
    {
        var edge = Read(NginxConfiguration);
        var configurations = new Dictionary<string, string>
        {
            ["nginx.conf and the locations both of its servers include"] =
                ActiveDirectives(EdgeAsAssembled()),
            ["pop.conf.template"] = ActiveDirectives(File.ReadAllText(
                Path.Combine(DockerDirectory, "nginx", "pop.conf.template"))),
        };

        // Only the headers more than one layer can set.
        var policyHeaders = new[]
        {
            "X-Frame-Options", "X-Content-Type-Options",
            "Referrer-Policy", "Permissions-Policy",
        };

        foreach (var (name, configuration) in configurations)
        {
            var blocks = HeaderBlocks(configuration);

            foreach (var block in blocks)
            {
                block.Added.Contains("Content-Type").Should().BeFalse(
                    $"{name} sets Content-Type with add_header in \"{block.Name}\": nginx emits " +
                    "one from default_type on a return with a body and the directive appends a " +
                    "second, and at location level it cancels the inheritance of every policy " +
                    "header above it as well");

                foreach (var hidden in block.Hidden)
                {
                    block.Emitted.Contains(hidden).Should().BeTrue(
                        $"{name} drops the incoming {hidden} in \"{block.Name}\" and sets none of " +
                        "its own, which leaves the response with no such policy at all");
                }
            }

            var emitting = blocks
                .SelectMany(block => block.Emitted
                    .Where(header => policyHeaders.Contains(header))
                    .Select(header => (Block: block, Header: header)))
                .ToList();

            emitting.Should().NotBeEmpty(
                $"{name} sets the policy headers of the origin it fronts");

            foreach (var (block, header) in emitting)
            {
                block.Hides(header).Should().BeTrue(
                    $"{name} emits {header} in \"{block.Name}\" and proxies to a layer that sets " +
                    "it too, so with no proxy_hide_header in scope the response leaves with two " +
                    "copies of one policy");
            }
        }
    }

    // A level keeps the directives above it only while it declares none of its own.
    private sealed class NginxBlock
    {
        // The block this one is nested in, or null at the top level.
        public NginxBlock? Parent { get; init; }

        // The line that opened the block, without its brace.
        public string Name { get; init; } = string.Empty;

        // Headers this block sets with an add_header of its own.
        public List<string> Added { get; } = new();

        // Headers this block drops with a proxy_hide_header of its own.
        public List<string> Hidden { get; } = new();

        // What a response leaving this block carries: its own headers, or the inherited set.
        public IReadOnlyList<string> Emitted =>
            Added.Count > 0 ? Added : Parent?.Emitted ?? Array.Empty<string>();

        public bool Hides(string header) => Hidden.Count > 0
            ? Hidden.Contains(header)
            : Parent?.Hides(header) ?? false;
    }

    // Splits a configuration into blocks by braces, keeping the two header directives of each;
    // lines still commented are commented on purpose.
    private static IReadOnlyList<NginxBlock> HeaderBlocks(string configuration)
    {
        var blocks = new List<NginxBlock>();
        var open = new Stack<NginxBlock>();

        foreach (var raw in configuration.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var current = open.Count > 0 ? open.Peek() : null;

            var added = Regex.Match(line, @"^add_header\s+([^\s;]+)");
            if (added.Success)
            {
                current?.Added.Add(added.Groups[1].Value);
            }

            var hidden = Regex.Match(line, @"^proxy_hide_header\s+([^\s;]+)");
            if (hidden.Success)
            {
                current?.Hidden.Add(hidden.Groups[1].Value);
            }

            if (line.EndsWith('{'))
            {
                var block = new NginxBlock { Parent = current, Name = line[..^1].Trim() };
                blocks.Add(block);
                open.Push(block);
            }
            else if (line == "}" && open.Count > 0)
            {
                open.Pop();
            }
        }

        return blocks;
    }
}
