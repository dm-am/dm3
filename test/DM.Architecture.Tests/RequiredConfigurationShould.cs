using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A value the deployment has to supply has no answer in the repository.
/// </summary>
/// <remarks>
/// Three hosts declare what they open - RequireRelationalStorage,
/// RequireDocumentStorage, RequireObjectStorage - and each of those refuses to
/// start on an empty value. The tracked appsettings.json answered all of them:
/// a connection string with a password in it and the object-store account, so
/// the check could never fire and a deployment that lost DM_ConnectionStrings__Rdb
/// started clean, answered its liveness probe, and failed on the first request
/// that touched data. The values live in the Development overlay now, which a
/// Production host does not read and the image does not carry.
///
/// Asserted on the files rather than on a built host, because what defeats the
/// check is the presence of a default, and a default is invisible from inside
/// the process that used it.
/// </remarks>
public class RequiredConfigurationShould
{
    /// <summary>Sections and keys a Require* call refuses to start without.</summary>
    private static readonly (string Section, string Key)[] Guarded =
    [
        ("ConnectionStrings", "Rdb"),
        ("CdnConfiguration", "AccessKey"),
        ("CdnConfiguration", "SecretKey"),
    ];


    /// <summary>
    /// Running from sources reaches the stack compose brings up, with the
    /// credentials compose brings it up with.
    /// </summary>
    /// <remarks>
    /// The development overlays name localhost and the ports of that stack, so
    /// they are not a separate environment - they are the same one, reached
    /// without the container. They had drifted from the template all the same: the
    /// password of the relational store, no credentials at all for the document
    /// store while it requires them, and the administrator account of the object
    /// store instead of the one scoped to the bucket.
    ///
    /// Nothing failed until somebody cloned the repository and ran the API from
    /// sources, and what they got was a connection refused with no hint that the
    /// answer was in a file two directories away.
    ///
    /// The template is the source: it is the file the installer copies to become
    /// the environment, so a value read from anywhere else is a fourth copy.
    /// </remarks>
    [Fact]
    public void ReachTheLocalStackWithTheCredentialsItIsBroughtUpWith()
    {
        var template = File.ReadAllText(Path.Combine(RepositoryRoot, "docker", ".env.example"));

        string Declared(string key)
        {
            var value = Regex.Match(template, $@"^{Regex.Escape(key)}=(.+)$", RegexOptions.Multiline);
            value.Success.Should().BeTrue($"{key} is what the stack is brought up with");
            return value.Groups[1].Value.Trim();
        }

        var overlays = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "appsettings.Development.json",
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
                settings.Should().Contain($"\"{Declared("MINIO_APP_USER")}\"",
                    $"{name} uses the account scoped to the bucket, not the administrator");
                settings.Should().Contain($"\"{Declared("MINIO_APP_PASSWORD")}\"",
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

    /// <summary>
    /// A settings file the SDK does not copy by itself has to be declared, and
    /// declared as the item type that SDK actually globs.
    /// </summary>
    /// <remarks>
    /// The mail worker declared its appsettings.json as a Content item under the
    /// plain SDK, where appsettings.json is a None item: the Update matched
    /// nothing, the file never reached the output, and the worker ran on whatever
    /// the environment supplied plus type defaults for everything else. In the
    /// stack that is every value; on a local run it is none of them.
    /// </remarks>
    [Fact]
    public void CopyTheSettingsOfEveryHostWhoseSdkDoesNotDoItAlone()
    {
        foreach (var host in Hosts)
        {
            var project = File.ReadAllText(Path.Combine(RepositoryRoot, "src", host, $"{host}.csproj"));
            if (project.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var settings in Directory
                         .EnumerateFiles(Path.Combine(RepositoryRoot, "src", host), "appsettings*.json")
                         .Select(Path.GetFileName))
            {
                project.Should().Contain($"<None Update=\"{settings}\">",
                    $"{host} is not a Web SDK project, where appsettings.json is a None item; " +
                    "a Content Update matches nothing there and the file silently stays out " +
                    "of the output");
            }
        }
    }

    /// <summary>
    /// The overlay stays out of the image.
    /// </summary>
    /// <remarks>
    /// This is the property the whole move rests on and the one nothing asserted:
    /// the guarded values were taken out of the base file and put into the
    /// Development overlay, which is harmless only as long as the overlay is not
    /// published. A Production host does not read it, so it would sit in the image
    /// unused — until someone runs the container with ASPNETCORE_ENVIRONMENT unset
    /// in a place that defaults it, and the deployment that lost its variables
    /// starts against localhost with a password of "admin" instead of refusing to
    /// start. Dropping CopyToPublishDirectory from any of the three projects put
    /// it back, with all four facts of this class green.
    /// </remarks>
    [Fact]
    public void PublishNoDevelopmentOverlayIntoTheImage()
    {
        var overlays = Hosts.Where(host => File.Exists(OverlaySettings(host))).ToList();

        overlays.Should().NotBeEmpty("the local defaults live in overlays now");
        foreach (var host in overlays)
        {
            var project = File.ReadAllText(Path.Combine(RepositoryRoot, "src", host, $"{host}.csproj"));
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
        Path.Combine(RepositoryRoot, "src", host, "appsettings.json");

    private static string OverlaySettings(string host) =>
        Path.Combine(RepositoryRoot, "src", host, "appsettings.Development.json");

    /// <summary>True when the file answers the key with a non-empty value.</summary>
    private static bool Reads(string path, string section, string key)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return document.RootElement.TryGetProperty(section, out var sectionElement) &&
               sectionElement.ValueKind == JsonValueKind.Object &&
               sectionElement.TryGetProperty(key, out var value) &&
               !string.IsNullOrEmpty(value.GetString());
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
