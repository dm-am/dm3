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

/// <summary>
/// The object store is deployed with the boundaries the code relies on.
/// </summary>
/// <remarks>
/// SeaweedFS runs as one process, and only its S3 port asks for a key: the
/// admin server, the filer, the volume server and their gRPC ports answer
/// anybody who reaches them and cannot be switched off. So the boundaries live
/// in three files rather than in the store, and each of them is a line somebody
/// can delete without anything failing on the stand: the internal network in
/// docker-compose.yml, the flags and the identity file in storage-start.sh, the
/// pinned client in the scripts that run it.
///
/// What answers on a stand that is up is checked by check-storage.sh, which reads
/// the deployment rather than a copy of it. What is asserted here is the half a
/// running store cannot show: that the lines holding the boundaries are still
/// written, before anything is started.
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

    /// <summary>
    /// The store sits in a network of its own, which nothing but its proxy joins.
    /// </summary>
    /// <remarks>
    /// On the stand network, imgproxy - the container that parses untrusted image
    /// data - could erase the bucket through the filer past every account. A port
    /// published or a second network attached reopens exactly that, and so does an
    /// overlay that declares the service again: compose merges its keys into these.
    /// </remarks>
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

    /// <summary>
    /// Every service the store can run without is off, and nothing reports home.
    /// </summary>
    /// <remarks>
    /// The IAM API would mint accounts beside the identity file. WebDAV, Iceberg
    /// and Lance are further doors on the same process, the admin UI is one more
    /// page nobody asked for, and telemetry is on unless it is turned off.
    /// </remarks>
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

    /// <summary>
    /// The command line is the whole configuration of the store.
    /// </summary>
    /// <remarks>
    /// The server writes the flags it was given into the volume and prefers the
    /// stored value whenever the command line holds the default. Without the file
    /// removed before each start, a flag taken out of the script keeps working for
    /// one more start and a default written out explicitly loses to the volume for
    /// good, so this file and the store would disagree. The volume size is one of
    /// those flags: left out, it is derived from the size of the disk.
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

    /// <summary>
    /// The collector is held under the limit of the container.
    /// </summary>
    /// <remarks>
    /// The filer allocates large long-lived buffers up front, and without a limit
    /// the collector lets the heap reach 233 MiB under load against 256M. The price
    /// of the limit is CPU at rest, which check-storage.sh measures; the limit
    /// itself is held here, together with the ceiling it was chosen against.
    /// </remarks>
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

    /// <summary>
    /// The store has no administrator, and every account is scoped to the bucket.
    /// </summary>
    /// <remarks>
    /// SeaweedFS turns an environment key into a global administrator that no
    /// bucket can scope, so the container holds none - neither written in nor
    /// carried over from docker/.env, where the offsite replication keys live under
    /// exactly those names. In the identity file an action without the bucket
    /// suffix applies to every bucket, and Admin creates and drops them.
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

    /// <summary>
    /// Anonymous reads reach the prefixes the product declared public, and no
    /// others.
    /// </summary>
    /// <remarks>
    /// One invariant, one owner. The prefixes are a product decision and live in
    /// UploadFolder; the grant is written into the identity file the store starts
    /// with, because the account the application runs as is deliberately not
    /// allowed to make one. A type added to the domain without the file following
    /// is a prefix nobody can read; a bucket-wide grant makes every later type
    /// public by default; and a List makes the random part of every key a lookup.
    ///
    /// Compared as sets of prefixes rather than by matching the text: the file
    /// spells them as patterns and the domain as folder names, and the point is
    /// that they name the same things.
    ///
    /// check-storage.sh proves the same split on a stand that is up, and it
    /// probes the prefixes it lists: a public type it does not list is one that
    /// nobody checks for reading or for listing, so its list is held here too.
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

    /// <summary>
    /// The images of the store and its client name a digest, and the client is
    /// written once.
    /// </summary>
    /// <remarks>
    /// A tag on Docker Hub can be rewritten, and the one-process mode of the store
    /// is young enough for its defaults to move between releases, so a new build
    /// under the same tag would change the store with no line of this repository
    /// changing. The client runs the backup, the boundary check and the restore
    /// line of the deployment guide, and two spellings of it would let one of
    /// them move while another stays behind.
    /// </remarks>
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

        var restore = Path.Combine(DM.Testing.RepositoryLayout.Root, "docs", "guides", "DEPLOYMENT.md");
        var clients = Directory
            .EnumerateFiles(DockerDirectory, "*.sh", SearchOption.AllDirectories)
            .Append(restore)
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

    /// <summary>An identity of the file storage-start.sh writes, by name and actions.</summary>
    private sealed record Identity(string Name, string[] Actions);

    /// <summary>
    /// The identities of the file the server is started with, parsed as JSON.
    /// </summary>
    /// <remarks>
    /// The heredoc puts every shell variable inside a JSON string, so the text is
    /// a valid document as written, and reading it as one leaves no room for a
    /// pattern to match something the server would read differently.
    /// </remarks>
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

    /// <summary>The value a script assigns to a variable at column zero, quotes removed.</summary>
    private static string ShellValue(string script, string name)
    {
        var assignment = Regex.Match(script, $@"(?m)^{name}=""([^""]*)""\r?$");
        assignment.Success.Should().BeTrue($"check-storage.sh declares {name} as a quoted constant");
        return assignment.Groups[1].Value;
    }

    /// <summary>The shell variable the bucket of the server is named by, such as <c>$B</c>.</summary>
    private static string BucketVariable()
    {
        var bucket = Regex.Match(string.Join(' ', StartCommand()), @"-bucket=""?(\$\w+)""?(\s|$)");
        bucket.Success.Should().BeTrue("the server creates the bucket named by a variable of the script");
        return bucket.Groups[1].Value;
    }

    /// <summary>
    /// The tokens of the command storage-start.sh hands the process over to, with
    /// its continuations folded.
    /// </summary>
    private static string[] StartCommand()
    {
        var folded = Regex.Replace(StartScript, @"\\\r?\n\s*", " ");
        var command = ActiveLines(folded).Where(line => line.StartsWith("exec ", StringComparison.Ordinal)).ToList();

        command.Should().ContainSingle("the script ends by handing the process over to the server");
        return command[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Trimmed lines, comments and blank lines left out.</summary>
    private static List<string> ActiveLines(string text) => text
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();

    /// <summary>The items of a YAML list under a key, as written.</summary>
    private static string[] ListUnder(List<string> lines, string key) => lines
        .SkipWhile(line => line != key)
        .Skip(1)
        .TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal))
        .Select(line => line[2..].Trim().Trim('\'', '"'))
        .ToArray();

    /// <summary>The body of one top-level section, up to the next one.</summary>
    private static string Section(string compose, string name) => Block(compose, name, indent: "");

    /// <summary>The body of one service.</summary>
    private static string ServiceBlock(string compose, string name) => Block(Section(compose, "services"), name);

    /// <summary>The body of one key at the given indent, up to the next key at that indent.</summary>
    private static string Block(string text, string name, string indent = "  ")
    {
        var lines = text.Split('\n');
        var start = Array.FindIndex(lines, line =>
            line.TrimEnd('\r') == $"{indent}{name}:" || line.StartsWith($"{indent}{name}: ", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, $"the compose file must declare {name}");
        return string.Join('\n', lines.Skip(start + 1)
            .TakeWhile(line => !Regex.IsMatch(line, $@"^{indent}[^\s#]")));
    }

    /// <summary>The services a compose file declares, by name.</summary>
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
