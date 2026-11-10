using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Each Require* refuses to start on an empty value, which a tracked default defeats; the values live
/// in the Development overlay. Asserted on the files: a default is invisible from inside the process.
/// </remarks>
public class RequiredConfigurationShould
{
    private static readonly (string Section, string Key)[] Guarded =
    [
        ("ConnectionStrings", "Rdb"),
        ("CdnConfiguration", "AccessKey"),
        ("CdnConfiguration", "SecretKey"),
    ];


    /// <remarks>
    /// The development overlays reach the compose stack without the container, and the template is the
    /// source.
    /// </remarks>
    [Fact]
    public void ReachTheLocalStackWithTheCredentialsItIsBroughtUpWith()
    {
        var template = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "docker", ".env.example"));

        string Declared(string key)
        {
            var value = Regex.Match(template, $@"^{Regex.Escape(key)}=(.+)$", RegexOptions.Multiline);
            value.Success.Should().BeTrue($"{key} is what the stack is brought up with");
            return value.Groups[1].Value.Trim();
        }

        var overlays = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "appsettings.Development.json",
                SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .ToArray();

        overlays.Should().HaveCountGreaterThanOrEqualTo(3,
            "the hosts that are run from sources each carry one, and a walk that finds none " +
            "passes on anything");

        foreach (var overlay in overlays)
        {
            var settings = File.ReadAllText(overlay);
            var name = Path.GetFileName(Path.GetDirectoryName(overlay)!);

            if (settings.Contains("\"Rdb\"", StringComparison.Ordinal))
            {
                settings.Should().Contain($"Password={Declared("DM_APP_PASSWORD")}",
                    $"{name} connects to the stack the template describes");
                settings.Should().NotContain("User ID=postgres",
                    $"{name} would hold the server rather than the database of this site");
            }

            if (settings.Contains("\"CdnConfiguration\"", StringComparison.Ordinal))
            {
                settings.Should().Contain($"\"{Declared("STORAGE_APP_USER")}\"",
                    $"{name} uses the account scoped to the bucket, not the administrator");
                settings.Should().Contain($"\"{Declared("STORAGE_APP_PASSWORD")}\"",
                    $"{name} uses the secret of that account");
            }
        }
    }

    private static readonly string[] Hosts =
        ["DM.Web.API", "DM.Workers.Mail", "DM.Workers.NotificationDispatcher", "DM.Tools.Seeder"];

    [Fact]
    public void FindTheFilesItReads() =>
        Hosts.Select(BaseSettings).Where(File.Exists).Should().HaveCount(Hosts.Length,
            "every host ships a tracked appsettings.json, and a walk that reads none passes");

    [Fact]
    public void LeaveEveryGuardedValueOutOfTheTrackedBaseFile() =>
        Hosts
            .SelectMany(host => Guarded
                .Where(guarded => Reads(BaseSettings(host), guarded.Section, guarded.Key))
                .Select(guarded => $"{host}: {guarded.Section}:{guarded.Key}"))
            .Should().BeEmpty(
                "the base file is read in every environment, so a value here satisfies the " +
                "ValidateOnStart written to catch its absence - and the deployment that " +
                "forgot the variable starts against localhost instead of refusing to start");

    /// <summary>
    /// The overlay is what keeps a fresh clone runnable, so it has to hold what
    /// was taken out of the base file.
    /// </summary>
    [Fact]
    public void KeepTheLocalDefaultsInTheDevelopmentOverlay()
    {
        var api = OverlaySettings("DM.Web.API");
        File.Exists(api).Should().BeTrue(
            "a local run reads it, and without it the developer meets the refusal meant " +
            "for a misconfigured server");

        foreach (var (section, key) in Guarded)
        {
            Reads(api, section, key).Should().BeTrue(
                $"{section}:{key} left the base file and has to be answered somewhere a " +
                "developer runs without arguments");
        }
    }

    // Under the plain SDK appsettings.json is a None item, so a Content Update matches nothing.
    [Fact]
    public void CopyTheSettingsOfEveryHostWhoseSdkDoesNotDoItAlone()
    {
        foreach (var host in Hosts)
        {
            var project = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "src", host, $"{host}.csproj"));
            if (project.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var settings in Directory
                         .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src", host), "appsettings*.json")
                         .Select(Path.GetFileName))
            {
                project.Should().Contain($"<None Update=\"{settings}\">",
                    $"{host} is not a Web SDK project, where appsettings.json is a None item; " +
                    "a Content Update matches nothing there and the file silently stays out " +
                    "of the output");
            }
        }
    }

    // An overlay in the image would start a deployment without variables against localhost.
    [Fact]
    public void PublishNoDevelopmentOverlayIntoTheImage()
    {
        var overlays = Hosts.Where(host => File.Exists(OverlaySettings(host))).ToList();

        overlays.Should().NotBeEmpty("the local defaults live in overlays now");
        foreach (var host in overlays)
        {
            var project = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "src", host, $"{host}.csproj"));
            var declaration = Regex.Match(
                project,
                @"<(?:None|Content) Update=""appsettings\.Development\.json"">(.*?)</(?:None|Content)>",
                RegexOptions.Singleline);

            declaration.Success.Should().BeTrue(
                $"{host} ships an appsettings.Development.json, and the item has to say what " +
                "happens to it on publish");
            declaration.Groups[1].Value.Should().Contain("<CopyToPublishDirectory>Never",
                $"{host} would otherwise carry its local connection strings and object-store " +
                "keys into the image, where the ValidateOnStart that guards their absence can " +
                "never fire");
        }
    }

    private static string BaseSettings(string host) =>
        Path.Combine(RepositoryLayout.Root, "src", host, "appsettings.json");

    private static string OverlaySettings(string host) =>
        Path.Combine(RepositoryLayout.Root, "src", host, "appsettings.Development.json");

    private static bool Reads(string path, string section, string key) =>
        !string.IsNullOrEmpty(Answer(path, section, key));

    private static string? Answer(string path, string section, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return document.RootElement.TryGetProperty(section, out var sectionElement) &&
               sectionElement.ValueKind == JsonValueKind.Object &&
               sectionElement.TryGetProperty(key, out var value)
            ? value.GetString()
            : null;
    }
}
