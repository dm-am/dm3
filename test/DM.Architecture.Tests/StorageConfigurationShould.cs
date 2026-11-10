using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Domain.Core.Uploads;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Only the S3 port of SeaweedFS asks for a key, so the boundaries live in compose, storage-start.sh and
/// the scripts. check-storage.sh checks a running stand; this, that the lines are still written.
/// </remarks>
public class StorageConfigurationShould
{
    private const string Server = "seaweedfs";
    private const string Proxy = "storage";
    private const string InternalNetwork = "storage-internal";
    private const string ClientImage = "amazon/aws-cli";

    private static string DockerDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "docker");

    private static string Compose => File.ReadAllText(Path.Combine(DockerDirectory, "docker-compose.yml"));

    private static string StartScript => File.ReadAllText(Path.Combine(DockerDirectory, "storage-start.sh"));

    // On a shared network any container could erase the bucket through the filer; an overlay merges
    // its keys in.
    [Fact]
    public void KeepTheStoreInAnInternalNetworkOnlyItsProxyJoins()
    {
        var server = ActiveLines(ServiceBlock(Compose, Server));

        server.Should().NotContain(line => line.StartsWith("ports:", StringComparison.Ordinal),
            "the services beside S3 answer without a password, so the store publishes nothing");
        server.Should().NotContain(line => line.StartsWith("network_mode:", StringComparison.Ordinal),
            "a shared network namespace puts the store wherever its owner is");
        ListUnder(server, "networks:").Should().Equal(new[] { InternalNetwork },
            "a second network makes every unauthenticated port of the store reachable from it");

        var network = ActiveLines(Block(Section(Compose, "networks"), InternalNetwork));
        network.Should().Contain("internal: true",
            "an internal network has no way out, and the store has no business outside");

        var members = ServiceNames(Compose)
            .Where(name => ActiveLines(ServiceBlock(Compose, name)).Any(line => line
                .TrimStart('-', ' ').Trim('\'', '"').TrimEnd(':') == InternalNetwork));
        members.Should().BeEquivalentTo(new[] { Proxy, Server },
            "the proxy is the one door, and a service that joins the network walks past it");

        foreach (var overlay in new[] { "docker-compose.preview.yml", "docker-compose.pop.yml" })
        {
            ServiceNames(File.ReadAllText(Path.Combine(DockerDirectory, overlay))).Should().NotContain(Server,
                $"{overlay} would merge its keys into the store, ports and networks included");
        }
    }

    // Each is another door on the same process, and telemetry is on unless turned off.
    [Theory]
    [InlineData("-s3.iam=false")]
    [InlineData("-webdav=false")]
    [InlineData("-admin.ui=false")]
    [InlineData("-s3.port.iceberg=0")]
    [InlineData("-s3.port.lance=0")]
    [InlineData("-master.telemetry=false")]
    public void StartTheStoreWithEverythingItDoesNotNeedSwitchedOff(string flag)
    {
        StartCommand().Should().Contain(flag,
            "the default of this flag opens a service the deployment has no use for");
    }

    /// <remarks>
    /// The server stores its flags in the volume and prefers them over a default on the command line, so the
    /// file is removed before each start. Without a volume size it is derived from the disk.
    /// </remarks>
    [Fact]
    public void StartTheStoreFromItsCommandLineAlone()
    {
        var lines = ActiveLines(StartScript);
        var removal = lines.IndexOf("rm -f /data/mini.options");
        var start = lines.FindIndex(line => line.StartsWith("exec ", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, "the script hands the process over to the server");
        removal.Should().BeGreaterThan(-1,
            "the flags stored in the volume would otherwise override the ones written here");
        removal.Should().BeLessThan(start, "a removal after exec never runs");

        StartCommand().Should().Contain("-master.volumeSizeLimitMB=128",
            "without it the size of a volume follows the size of the disk it lands on");
    }

    // The filer allocates large buffers up front; check-storage.sh measures the CPU price.
    [Fact]
    public void HoldTheHeapOfTheStoreUnderTheLimitOfItsContainer()
    {
        var server = ActiveLines(ServiceBlock(Compose, Server));

        server.Should().Contain("GOMEMLIMIT: 180MiB",
            "an unlimited collector brings the peak within 23 MiB of the container limit, " +
            "and a process killed for memory drops every upload in flight");
        server.Should().Contain("memory: 256M",
            "the heap limit was chosen against this ceiling and means nothing under another");
    }

    /// <remarks>
    /// An environment key becomes a global administrator no bucket can scope; an action without the bucket
    /// suffix applies to every bucket.
    /// </remarks>
    [Fact]
    public void GiveTheStoreNoAdministrator()
    {
        var server = ActiveLines(ServiceBlock(Compose, Server));
        server.Should().NotContain(line => line.Contains("AWS_", StringComparison.Ordinal),
            "an environment key is an administrator of every bucket");
        server.Should().NotContain(line => line.StartsWith("env_file:", StringComparison.Ordinal),
            "docker/.env holds AWS keys of its own, and an env_file hands them to the store");

        var bucket = BucketVariable();
        var actions = Identities().SelectMany(identity => identity.Actions).ToList();

        actions.Should().NotBeEmpty("the identity file grants every right the workloads hold");
        foreach (var action in actions)
        {
            action.Should().MatchRegex($@"^[A-Za-z]+:{Regex.Escape(bucket)}(/.+)?$",
                "an action without the bucket suffix applies to every bucket of the store");
            action.Should().NotStartWith("Admin:", "Admin creates and drops buckets");
        }
    }

    /// <remarks>
    /// The prefixes live in UploadFolder and the grant in the identity file, compared as sets. The list
    /// check-storage.sh probes is held here too.
    /// </remarks>
    [Fact]
    public void GrantAnonymousReadsOnlyToThePrefixesTheProductDeclaredPublic()
    {
        var declared = UploadFolder.AnonymouslyReadable
            .Select(UploadFolder.For)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToArray();
        declared.Should().NotBeEmpty("the rule below is written in terms of those prefixes");

        var anonymous = Identities().Where(identity => identity.Name == "anonymous").ToList();
        anonymous.Should().ContainSingle("the store gives unsigned requests the rights of this one identity");

        var pattern = new Regex($@"^Read:{Regex.Escape(BucketVariable())}/([^/*]+)/\*$");
        foreach (var action in anonymous[0].Actions)
        {
            action.Should().MatchRegex(pattern.ToString(),
                "an anonymous right is a read under one prefix: a bucket-wide read makes every " +
                "later type public by default, and listing turns the random part of a key into a lookup");
        }

        var granted = anonymous[0].Actions
            .Select(action => pattern.Match(action).Groups[1].Value)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToArray();
        granted.Should().Equal(declared,
            "UploadFolder.AnonymouslyReadable is where the decision is made, and a type " +
            "added to it without the identity file following is a prefix nobody can read");

        var check = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "check-storage.sh"));
        var probed = ShellValue(check, "PUBLIC_PREFIXES")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToArray();
        probed.Should().Equal(declared,
            "the live check proves each public prefix readable and unlisted, and one it does " +
            "not name goes unchecked on every stand");
        declared.Should().NotContain(ShellValue(check, "PRIVATE_PREFIX"),
            "the private probe has to be a prefix nobody may read");
    }

    // A Docker Hub tag can be rewritten, and the client runs backup, check and restore from one
    // spelling.
    [Fact]
    public void PinTheStoreAndItsClientByDigest()
    {
        const string digested = @"^[^\s@]+:[\w.-]+@sha256:[0-9a-f]{64}$";

        foreach (var service in new[] { Server, Proxy })
        {
            var image = ActiveLines(ServiceBlock(Compose, service))
                .Single(line => line.StartsWith("image:", StringComparison.Ordinal))[6..]
                .Trim().Trim('\'', '"');
            image.Should().MatchRegex(digested, $"the image of {service} can be rewritten under its tag");
        }

        var restore = Path.Combine(DockerDirectory, "scripts", "restore-storage.sh");
        var clients = Directory
            .EnumerateFiles(DockerDirectory, "*.sh", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), $@"{Regex.Escape(ClientImage)}[^\s'""`\\)]*"))
            .Select(match => match.Value)
            .ToList();

        clients.Should().HaveCountGreaterThan(1,
            "the backup and the boundary check both run the client, and a walk that finds " +
            "neither checks nothing");
        File.ReadAllText(restore).Should().Contain(ClientImage,
            "the restore runs the client the backup wrote its copy with");
        clients.Distinct().Should().ContainSingle(
            "the scripts and the restore that run the client have to run the same one");
        clients[0].Should().MatchRegex(digested, "the client image can be rewritten under its tag");
    }

    private sealed record Identity(string Name, string[] Actions);

    // The heredoc keeps every variable inside a JSON string, so the text is a valid document.
    private static List<Identity> Identities()
    {
        var config = Regex.Match(string.Join(' ', StartCommand()), @"-s3\.config=(\S+)");
        config.Success.Should().BeTrue("the server reads its accounts from the file named on its command line");

        var heredoc = Regex.Match(StartScript,
            $@"^cat > {Regex.Escape(config.Groups[1].Value)} <<'?(\w+)'?\r?\n(?<body>[\s\S]*?)\r?\n\1\r?$",
            RegexOptions.Multiline);
        heredoc.Success.Should().BeTrue("storage-start.sh writes the file the server is told to read");

        using var document = JsonDocument.Parse(heredoc.Groups["body"].Value);
        return document.RootElement.GetProperty("identities").EnumerateArray()
            .Select(identity => new Identity(
                identity.GetProperty("name").GetString()!,
                identity.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!).ToArray()))
            .ToList();
    }

    private static string ShellValue(string script, string name)
    {
        var assignment = Regex.Match(script, $@"(?m)^{name}=""([^""]*)""\r?$");
        assignment.Success.Should().BeTrue($"check-storage.sh declares {name} as a quoted constant");
        return assignment.Groups[1].Value;
    }

    private static string BucketVariable()
    {
        var bucket = Regex.Match(string.Join(' ', StartCommand()), @"-bucket=""?(\$\w+)""?(\s|$)");
        bucket.Success.Should().BeTrue("the server creates the bucket named by a variable of the script");
        return bucket.Groups[1].Value;
    }

    private static string[] StartCommand()
    {
        var folded = Regex.Replace(StartScript, @"\\\r?\n\s*", " ");
        var command = ActiveLines(folded).Where(line => line.StartsWith("exec ", StringComparison.Ordinal)).ToList();

        command.Should().ContainSingle("the script ends by handing the process over to the server");
        return command[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static List<string> ActiveLines(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();

    private static string[] ListUnder(List<string> lines, string key) => lines
        .SkipWhile(line => line != key)
        .Skip(1)
        .TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal))
        .Select(line => line[2..].Trim().Trim('\'', '"'))
        .ToArray();

    private static string Section(string compose, string name) => Block(compose, name, indent: "");

    private static string ServiceBlock(string compose, string name) => Block(Section(compose, "services"), name);

    private static string Block(string text, string name, string indent = "  ")
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, line =>
            line.TrimEnd('\r') == $"{indent}{name}:" || line.StartsWith($"{indent}{name}: ", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, $"the compose file must declare {name}");
        return string.Join('\n', lines.Skip(start + 1)
            .TakeWhile(line => !Regex.IsMatch(line, $@"^{indent}[^\s#]")));
    }

    private static IEnumerable<string> ServiceNames(string compose)
    {
        if (!Regex.IsMatch(compose, @"(?m)^services:\s*$"))
        {
            return [];
        }

        return Regex.Matches(Section(compose, "services"), @"(?m)^  ([\w.-]+):\s*$")
            .Select(match => match.Groups[1].Value);
    }
}
