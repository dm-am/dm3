using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// Which credentials the deployment holds, who holds them, and where they are
/// never written.
/// </summary>
/// <remarks>What a leaked configuration costs is decided by the account in it.</remarks>
public class DeploymentCredentialsShould
{
    /// <remarks>
    /// The superuser buys the server and COPY FROM PROGRAM, a database role one database. The
    /// connection strings and the script are read together; since Postgres 15 only the database
    /// owner creates in the public schema.
    /// </remarks>
    [Fact]
    public void ReachPostgresAsARoleSomebodyCreatedForIt()
    {
        var compose = Read(BaseCompose);

        var workload = Regex.Match(compose, @"DM_ConnectionStrings__Rdb:\s*(.+)");
        workload.Success.Should().BeTrue("the workloads share one connection string");
        workload.Value.Should().NotContain("User ID=postgres",
            "a workload holding the superuser holds the server rather than this database");

        var exporter = Regex.Match(compose, @"DATA_SOURCE_NAME:\s*(.+)");
        exporter.Success.Should().BeTrue("the exporter has one of its own");
        exporter.Value.Should().NotContain("postgresql://postgres",
            "reading the statistics views needs pg_monitor and nothing else");

        var script = InitScriptStatements();

        foreach (var role in new[] { "dm_app", "dm_exporter" })
        {
            script.Should().Contain($"CREATE ROLE {role}",
                $"{role} is used by the deployment and created by nobody");
        }

        script.Should().Contain("OWNER dm_app",
            "a database owned by the superuser leaves the application unable to create its " +
            "own tables in the public schema");
        script.Should().Contain("GRANT pg_monitor TO dm_exporter",
            "the exporter reads the statistics views, and that is a predefined role");

        ServiceBlock(compose, "postgres").Should().Contain("postgres-init.sh:/docker-entrypoint-initdb.d/",
            "a script the server never runs creates nothing at all");
    }

    /// <summary>
    /// Nobody but the application role may connect to the application database, and the
    /// exporter reads the server through the maintenance database.
    /// </summary>
    /// <remarks>
    /// Any role that connects to a database hears every notification in it and sends its own.
    /// The revoke runs once, on an empty volume; the smoke job reads it back.
    /// </remarks>
    [Fact]
    public void LetOnlyTheApplicationRoleConnectToTheApplicationDatabase()
    {
        var script = InitScriptStatements();

        var created = script.IndexOf("CREATE DATABASE dm3", StringComparison.Ordinal);
        var revoked = script.IndexOf("REVOKE CONNECT ON DATABASE dm3 FROM PUBLIC", StringComparison.Ordinal);
        created.Should().BeGreaterThan(-1, "the script creates the application database");
        revoked.Should().BeGreaterThan(created,
            "every role may connect to a new database until the right is taken from PUBLIC");

        // pg_up and pg_stat_activity_count, which the alert and the dashboard read, cover the
        // whole server from any database the exporter reaches.
        var exporter = Regex.Match(Read(BaseCompose), @"DATA_SOURCE_NAME:\s*""?postgresql://[^/]+/(?<database>\w+)");
        exporter.Success.Should().BeTrue("the exporter connects through one connection string");
        exporter.Groups["database"].Value.Should().Be("postgres",
            "an exporter connected to dm3 is a second role that hears and sends pushes");

        // The script holds only on a fresh volume, so the deployed pair reads the right back.
        var smoke = ServiceBlock(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml")),
            "deployment-smoke");
        smoke.Should().Contain("bash docker/scripts/check-database-privileges.sh",
            "a restore into an existing database leaves PUBLIC its right, and only a read of " +
            "the server tells");
        File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "check-database-privileges.sh"))
            .Should().Contain("has_database_privilege('public', 'dm3', 'CONNECT')",
                "the check asks the server, not the script that was meant to set it");
    }

    /// <summary>
    /// Each workload holds the scoped object store account storage-start.sh declares for it, and
    /// the store hands out no account that can drop the bucket.
    /// </summary>
    [Fact]
    public void GiveNoWorkloadTheObjectStoreRootAccount()
    {
        var compose = Read(BaseCompose);

        FindValue(compose, "DM_CdnConfiguration__AccessKey:").Should().Be("${STORAGE_APP_USER:-dm-app}",
            "the application gets a service account scoped to its bucket, not an administrator");
        FindValue(compose, "DM_CdnConfiguration__SecretKey:").Should().StartWith("${STORAGE_APP_PASSWORD:?",
            "and the secret of that account, demanded at start");
    }

    /// <summary>
    /// Every credential the workloads hold can be given to them through
    /// docker/.env, and the template names the variable that gives it.
    /// </summary>
    /// <remarks>docker/.env.example is where the operator learns what the stack can be given.</remarks>
    [Fact]
    public void LetTheEnvironmentFileSupplyEveryWorkloadCredential()
    {
        var lines = Read(BaseCompose).Split('\n');
        var template = Read(".env.example");

        var start = Array.FindIndex(lines, line => line.StartsWith("x-workload-env:", StringComparison.Ordinal));
        start.Should().BeGreaterThan(-1, "the workloads share one environment anchor");

        var credentials = lines
            .Skip(start + 1)
            .TakeWhile(line => line.Length == 0 || line[0] is ' ' or '#')
            .Select(line => Regex.Match(line,
                @"^\s+(?<key>DM_\w+?__\w*(?:Username|Password|Secret|Key|Salt|Token)\w*)\s*:(?<value>.*)$"))
            .Where(match => match.Success)
            .ToList();

        credentials.Should().HaveCountGreaterThanOrEqualTo(5,
            "the anchor carries the keys of the store, the signature, the relay and the bots, " +
            "and a walk that finds none passes on anything");

        foreach (var credential in credentials)
        {
            var key = credential.Groups["key"].Value;
            var value = credential.Groups["value"].Value.Trim();

            value.Should().NotBeEmpty(
                $"{key} written as an empty literal is a credential no environment file can supply");

            foreach (Match substitution in Regex.Matches(value, @"\$\{(?<name>\w+)"))
            {
                var name = substitution.Groups["name"].Value;
                template.Should().MatchRegex($@"(?m)^#?\s*{name}=",
                    $"{key} comes from {name}, and docker/.env.example is where the operator " +
                    "looks for what the stack can be given");
            }
        }
    }

    /// <summary>
    /// The preview credentials are generated on the host, by one script, with a
    /// hash that survives being leaked.
    /// </summary>
    /// <remarks>apr1 is a thousand rounds of md5; the generated file stays off the repository.</remarks>
    [Fact]
    public void GenerateThePreviewCredentialsFromOnePlace()
    {
        var generator = Path.Combine(DockerDirectory, "scripts", "init-htpasswd.sh");
        File.Exists(generator).Should().BeTrue(
            "the installer and the operator changing the password call the same script");

        File.ReadAllText(generator).Should().Contain("htpasswd -niB",
            "bcrypt rather than the apr1 default, and the password over stdin rather " +
            "than in an argument every process listing shows");

        File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "install-release.sh"))
            .Should().Contain("init-htpasswd.sh",
                "a release carries no credentials, so the installer has to create them");

        File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".gitignore"))
            .Should().Contain("docker/nginx/.htpasswd",
                "the generated file lands inside the tree compose mounts it from");

        var generatorPath = Path.GetFullPath(generator);
        var duplicates = Directory
            .EnumerateFiles(DockerDirectory, "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Path.Combine(RepositoryLayout.Root, "docs"), "*.md", SearchOption.AllDirectories))
            .Where(path => !string.Equals(
                Path.GetFullPath(path), generatorPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("htpasswd -", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))
            .ToList();

        duplicates.Should().BeEmpty(
            "a second copy of the command is how the installer and the guide both kept " +
            "generating apr1 long after the choice had been made once");
    }

    // .gitignore has no effect on a path git already tracks; asserted over the whole list.
    [Fact]
    public void TrackNoFileTheIgnoreListClaimsToIgnore()
    {
        var tracked = Git("ls-files --cached --ignored --exclude-standard");

        tracked.Should().BeEmpty(
            "a tracked file that .gitignore names is in every clone and stays in the " +
            "history, while the entry tells the next author that it is not");
    }

    // A printed password stays in the terminal, the session history and the install log.
    [Fact]
    public void PrintNoCredentialFromTheInstaller()
    {
        var lines = File.ReadAllLines(Path.Combine(DockerDirectory, "setup-server.sh"))
            .Concat(File.ReadAllLines(Path.Combine(DockerDirectory, "scripts", "install-release.sh")))
            .Where(line => line.TrimStart().StartsWith("echo", StringComparison.Ordinal))
            .ToList();

        lines.Should().NotBeEmpty("the installer still reports what it did");
        lines.Should().NotContain(
            line => line.Contains("DM_PREVIEW_PASSWORD", StringComparison.Ordinal),
            "the terminal, the shell history and the install log keep whatever is printed here");
    }

    // Without the comments: this file explains itself at length, and a rule
    // that reads the prose is satisfied by a statement somebody commented out.
    private static string InitScriptStatements() => string.Join('\n', File
        .ReadAllLines(Path.Combine(DockerDirectory, "postgres-init.sh"))
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
        .Where(line => !line.StartsWith('#')));

    private static string Git(string arguments)
    {
        var (exitCode, output, error) = RepositoryFiles.Run("git", arguments);

        exitCode.Should().Be(0, $"git {arguments} failed: {error}");
        return output.Trim();
    }
}
