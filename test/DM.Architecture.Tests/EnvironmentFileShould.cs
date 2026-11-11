using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// docker/.env exists, complete and not the repository's, before anything that
/// needs it starts.
/// </summary>
/// <remarks>init-env.sh is the one place that creates the file; DEPLOYMENT.md, section "Переменные окружения".</remarks>
public class EnvironmentFileShould
{
    private const string EnvironmentTemplate = ".env.example";

    // Compose declares the key and the store accounts through ${...:?}, which rejects an empty
    // value.
    [Fact]
    public void CreateTheEnvironmentFileBeforeTheInstallerStartsTheStack()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var generator = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var creation = installer.IndexOf("init-env.sh", StringComparison.Ordinal);
        var start = installer.IndexOf("docker compose", StringComparison.Ordinal);

        creation.Should().BeGreaterThan(-1, "nothing else creates docker/.env on a clean server");
        start.Should().BeGreaterThan(-1, "the installer is still the thing that starts the stack");
        creation.Should().BeLessThan(start, "compose stops on interpolation before it starts anything");

        generator.Should().Contain("DM_CryptoConfiguration__KeyBase64",
            "the one value the template leaves empty is the one compose refuses to start without");
        generator.Should().Contain("openssl rand",
            "a key inherited from the repository is not a secret");
    }

    // The template ships the encryption key empty; asked of both scripts.
    [Theory]
    [InlineData("dm.sh", "docker compose up")]
    [InlineData("dm.ps1", "compose up")]
    public void CreateTheEnvironmentFileBeforeTheDeveloperScriptStartsTheStack(
        string name, string startsWith)
    {
        var script = ManagementScriptsShould.Read(name);

        var creation = script.IndexOf("init-env.sh", StringComparison.Ordinal);
        var start = script.IndexOf(startsWith, StringComparison.Ordinal);

        creation.Should().BeGreaterThan(-1,
            $"{name} has to reach the generator, which is the one place that creates " +
            "docker/.env; a second copy of that step is how the two platforms drifted " +
            "apart in the first place");
        start.Should().BeGreaterThan(-1, $"{name} is still the thing that starts the stack");
        creation.Should().BeLessThan(start,
            "compose stops on interpolation before it starts anything");

        script.Should().NotContainAny(
            [".env.example\" \"$ENV", "Copy-Item", "cp \"$DOCKER_DIR/.env.example"],
            $"{name} copying the template itself is the second implementation, and it is " +
            "the half that never learns what the generator learns next");
    }

    // A hand-copied .env.example carries the encryption key empty.
    [Fact]
    public void CompleteAnEnvironmentFileSomebodyElseCreated()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        generator.Should().NotContain("already exists at",
            "an existing file is topped up, not accepted as it is: the key is what is " +
            "missing from a hand-made copy and the only thing compose refuses to start " +
            "without");
        generator.Should().Contain("set_if_empty",
            "and topping up fills only what is empty, so rerunning it never invalidates " +
            "a key a running stand already encrypts with");
    }

    /// <summary>
    /// A server never comes up in Development, and never on the passwords printed
    /// in this repository.
    /// </summary>
    /// <remarks>
    /// The environment is read at startup and simply set; a password is baked into the Postgres
    /// users at first boot, so the script refuses instead of rotating it.
    /// </remarks>
    [Fact]
    public void RefuseToApproveAServerFileStillHoldingTheTemplatesSecrets()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var environmentBlock = generator
            .Split("set_value ASPNETCORE_ENVIRONMENT Production", StringSplitOptions.None);
        environmentBlock.Should().HaveCount(2,
            "the environment is set in exactly one place");

        // Asserted as "does not test EXISTING", whatever the condition is.
        var guard = environmentBlock[0][(environmentBlock[0].LastIndexOf("if [", StringComparison.Ordinal))..];
        guard.Should().NotContain("EXISTING",
            "setting the environment only on a file this run created is what left a " +
            "hand-copied server .env running in Development");
        guard.Should().Contain("\"$MODE\" = \"server\"",
            "and it is a server the rule is about");

        generator.Should().Contain("still holds the example values for",
            "an existing server file carrying the repository's passwords has to be " +
            "refused, not noted");
        generator.Should().MatchRegex(@"still holds the example values for[\s\S]{0,400}exit 1",
            "and refused with a non-zero code: a note on stderr beside exit 0 reads " +
            "as success to whatever called this");
    }

    /// <remarks>
    /// A password sits between the colon and the at sign of a connection string. The template is what
    /// the CI jobs and the first run copy.
    /// </remarks>
    [Fact]
    public void GiveTheTemplateAPasswordThatSurvivesBeingPutInAUri()
    {
        var compose = Read(BaseCompose);
        var template = Read(EnvironmentTemplate);

        // scheme://user:${VAR}@host, the user part a whole ${...} or a plain character.
        var embedded = Regex.Matches(compose, @"://(?:\$\{[^}]*\}|[^\s:@/])*:\$\{(?<name>\w+)[^}]*\}@")
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToList();

        embedded.Should().Contain("DM_EXPORTER_PASSWORD",
            "the exporter's connection string puts a password between a colon and an at " +
            "sign, and an extraction that misses it passes on anything");

        foreach (var name in embedded)
        {
            var value = Regex.Match(template, $@"^{name}=(?<value>.*)$", RegexOptions.Multiline);

            value.Success.Should().BeTrue(
                $"{name} reaches a URI and docker/.env.example is what the CI jobs and the " +
                "documented first run copy");

            // RFC 3986 unreserved and sub-delims; nothing on the way in percent-encodes.
            value.Groups["value"].Value.Should().MatchRegex(@"^[A-Za-z0-9\-._~!$&'()*+,;=]+$",
                $"{name} is substituted between the colon and the at sign of a connection " +
                "string, so a character that a URI reads as a delimiter ends the string early " +
                "and the client rejects it before it connects");
        }
    }

    /// <remarks>
    /// A secret nothing reads is one more value to generate, copy, rotate and leak, and the first
    /// reader to look for its service finds none. Mutation it must fail on: the password of a
    /// service that left the stack kept in the generator, the template or the placeholders of the
    /// compose checks.
    /// </remarks>
    [Theory]
    [InlineData("scripts/init-env.sh", @"(?<=SECRETS=\([^)]*)\b[A-Z][A-Z0-9_]+\b(?=[^)]*\))")]
    [InlineData(EnvironmentTemplate, @"(?m)^[A-Za-z][A-Za-z0-9_]*(?==)")]
    [InlineData("../scripts/lib/compose-placeholders.sh", @"(?m)(?<=^export )[A-Za-z][A-Za-z0-9_]*(?==)")]
    public void CarryOnlyVariablesTheStackReads(string file, string names)
    {
        var compose = string.Concat(Directory
            .EnumerateFiles(DockerDirectory, "docker-compose*.yml")
            .Select(File.ReadAllText));
        var declared = Regex.Matches(Read(file), names).Select(match => match.Value).Distinct().ToList();

        declared.Should().NotBeEmpty($"{file} sets variables, and a pattern that finds none passes everything");
        declared.Where(name => !compose.Contains("${" + name, StringComparison.Ordinal))
            .Should().BeEmpty($"{file} sets variables no compose file reads, so they belong to nothing the stack runs");
    }
}
