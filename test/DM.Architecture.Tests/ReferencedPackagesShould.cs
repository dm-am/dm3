using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Read from text, nothing restored or loaded: a package counts as used when the project's own files
/// show it by a pattern of <see cref="Hints"/>, or by its id as a namespace. Test projects are left
/// out of the evidence search. A finding the hints cannot explain is a failure: the answer is to
/// drop the reference, or to add the pattern that proves its use, never a bare excuse.
/// </remarks>
public class ReferencedPackagesShould
{
    /// <summary>
    /// What shows a package in use when its namespace is not its id.
    /// </summary>
    /// <param name="Patterns">Regular expressions, any one of which in the project's files is the evidence.</param>
    private sealed record Hint(string Package, string[] Patterns);

    /// <summary>
    /// A package that is restored for tooling and never shows in code, or a central
    /// version that no project references: either way, why it stays.
    /// </summary>
    /// <param name="Why">Why it is needed although nothing names it.</param>
    private sealed record Kept(string Package, string Why);

    private static readonly Hint[] Hints =
    [
        new("AWSSDK.S3", [@"\bAmazon\.S3\b", @"\bAmazon\.Runtime\b"]),
        new("AspNetCore.HealthChecks.Npgsql", [@"\bAddNpgSql\b", @"\bNpgSqlHealthCheck"]),
        new("AspNetCore.HealthChecks.UI.Client", [@"\bUIResponseWriter\b", @"HealthChecks\.UI\.Client"]),
        new("BouncyCastle.Cryptography", [@"\bOrg\.BouncyCastle\b"]),
        new("Konscious.Security.Cryptography.Argon2", [@"\bKonscious\.Security\.Cryptography\b", @"\bArgon2id\b"]),
        new("Magick.NET-Q8-AnyCPU", [@"\bImageMagick\b", @"\bMagickImage\b"]),
        new("Microsoft.Extensions.Caching.Memory",
            [@"\bAddMemoryCache\b", @"\bMemoryCache\b", @"\bMicrosoft\.Extensions\.Caching\.Memory\b"]),
        new("Microsoft.Extensions.Configuration.EnvironmentVariables", [@"\bAddEnvironmentVariables\b"]),
        new("Microsoft.Extensions.Configuration.Json", [@"\bAddJsonFile\b", @"\bAddJsonStream\b"]),
        new("Microsoft.Extensions.DependencyInjection.Abstractions",
            [@"\bMicrosoft\.Extensions\.DependencyInjection\b", @"\bIServiceCollection\b"]),
        new("Microsoft.Extensions.Hosting.Abstractions",
            [@"\bMicrosoft\.Extensions\.Hosting\b", @"\bIHostedService\b", @"\bBackgroundService\b", @"\bIHostEnvironment\b"]),
        new("Microsoft.Extensions.Logging.Abstractions", [@"\bMicrosoft\.Extensions\.Logging\b", @"\bILogger\b"]),
        new("Microsoft.Extensions.Options",
            [@"\bMicrosoft\.Extensions\.Options\b", @"\bIOptions", @"\bOptionsBuilder\b"]),
        new("Microsoft.Extensions.Options.ConfigurationExtensions",
            [@"\.Configure<", @"\.Bind\(", @"\bBindConfiguration\b"]),
        new("Npgsql.EntityFrameworkCore.PostgreSQL", [@"\bUseNpgsql\b", @"\bNpgsql\b"]),
        new("OpenTelemetry.Exporter.OpenTelemetryProtocol", [@"\bAddOtlpExporter\b", @"\bOpenTelemetry\.Exporter\b"]),
        new("OpenTelemetry.Exporter.Prometheus.AspNetCore",
            [@"\bAddPrometheusExporter\b", @"\bMapPrometheusScrapingEndpoint\b", @"\bUseOpenTelemetryPrometheusScrapingEndpoint\b"]),
        new("OpenTelemetry.Extensions.Hosting", [@"\bAddOpenTelemetry\b", @"\bWithMetrics\b", @"\bWithTracing\b"]),
        new("OpenTelemetry.Instrumentation.AspNetCore", [@"\bAddAspNetCoreInstrumentation\b"]),
        new("OpenTelemetry.Instrumentation.EntityFrameworkCore", [@"\bAddEntityFrameworkCoreInstrumentation\b"]),
        new("OpenTelemetry.Instrumentation.Http", [@"\bAddHttpClientInstrumentation\b"]),
        new("OpenTelemetry.Instrumentation.Runtime", [@"\bAddRuntimeInstrumentation\b"]),
        new("Otp.NET", [@"\bOtpNet\b"]),
        new("Riok.Mapperly", [@"\bRiok\.Mapperly\b", @"\[Mapper\b"]),
        // The three hosts call UseSerilog and get the package through DM.Infrastructure.Core; the
        // project itself shows it by the request logging it wires.
        new("Serilog.AspNetCore", [@"\bUseSerilog\b", @"\bUseSerilogRequestLogging\b", @"\bSerilog\.AspNetCore\b"]),
        new("Serilog.Formatting.Compact",
            [@"\bCompactJsonFormatter\b", @"\bRenderedCompactJsonFormatter\b", @"\bSerilog\.Formatting\.Compact\b"]),
        new("Serilog.Sinks.Console", [@"WriteTo\.Console\b", @"\.Console\(", "\"Console\""]),
        new("Serilog.Sinks.Grafana.Loki", [@"\bGrafanaLoki\b"]),
        new("Swashbuckle.AspNetCore", [@"\bSwashbuckle\b", @"\bAddSwaggerGen\b", @"\bUseSwagger"]),
    ];

    private static readonly Kept[] Tooling =
    [
        new("Microsoft.EntityFrameworkCore.Design",
            "Design-time tooling for dotnet ef, never named in code; it goes with the last design-time factory."),
        new("Microsoft.NET.Test.Sdk", "The test host entry point every test project needs."),
        new("xunit.runner.visualstudio", "The adapter dotnet test finds the tests through."),
        new("coverlet.collector", "The data collector the CI test step gathers coverage with."),
        new("NSubstitute.Analyzers.CSharp", "An analyzer: it reports through the compiler and never ships."),
    ];

    private static readonly Kept[] Unreferenced =
    [
        new("System.Text.Json", "Same-version guard: no branch of the graph may drag it below the BCL version."),
        new("Microsoft.EntityFrameworkCore.Relational",
            "Transitive pin: the Npgsql provider brings it in, and the pin keeps it level with Microsoft.EntityFrameworkCore."),
        new("Microsoft.Extensions.Configuration.FileExtensions",
            "Transitive pin: Configuration.Json brings it in, and the pin keeps it level with the rest of the family."),
        new("Microsoft.Extensions.Diagnostics.HealthChecks",
            "Transitive pin: without it the graph resolves 8.0.11 instead of 10.0.11 in ten projects."),
        new("System.Security.Cryptography.Pkcs", "Transitive via MimeKit, pinned above GHSA-846f-f764-76mx."),
        new("Swashbuckle.AspNetCore.Swagger",
            "Transitive pin: a part of Swashbuckle.AspNetCore, held at its version so the parts move with it."),
        new("Swashbuckle.AspNetCore.SwaggerUI",
            "Transitive pin: a part of Swashbuckle.AspNetCore, held at its version so the parts move with it."),
        new("SSH.NET", "Testcontainers asks for 2025.1.0, which GHSA-q939-rpr3-3284 covers; the fix is in 2026.0.0."),
    ];

    private static readonly string[] EvidenceExtensions =
        [".cs", ".cshtml", ".razor", ".json", ".props", ".targets", ".yml", ".yaml"];

    private sealed record Project(string File, string Directory, string Name, string[] Packages, string[] Projects);

    private static readonly Lazy<IReadOnlyList<Project>> AllProjects = new(ReadProjects);

    [Fact]
    public void UseEveryPackageTheyReference()
    {
        var hints = Hints.ToDictionary(hint => hint.Package, StringComparer.Ordinal);
        var tooling = Tooling.Select(kept => kept.Package).ToHashSet(StringComparer.Ordinal);

        var unused = new List<string>();
        foreach (var project in ProductProjects())
        {
            var text = Text(project, withJson: true);
            foreach (var package in project.Packages.Where(package => !tooling.Contains(package)))
            {
                var patterns = hints.TryGetValue(package, out var hint)
                    ? hint.Patterns
                    : [$@"\b{Regex.Escape(package)}\b"];
                if (!patterns.Any(pattern => Regex.IsMatch(text, pattern)))
                {
                    unused.Add($"{project.File}: {package} (searched for {string.Join("  ", patterns)})");
                }
            }
        }

        string.Join(Environment.NewLine, unused).Should().BeEmpty(
            "a package the project never names is either dead or used in a way nobody wrote " +
            "down: remove the reference, or add to Hints the pattern that proves its use");
    }

    [Fact]
    public void UseEveryProjectTheyReference()
    {
        var byFile = AllProjects.Value.ToDictionary(project => project.File, StringComparer.Ordinal);
        var unused = new List<string>();
        foreach (var project in ProductProjects())
        {
            var text = Text(project, withJson: false);
            foreach (var reference in project.Projects)
            {
                byFile.Should().ContainKey(reference, $"{project.File} references a project of the tree");
                var name = byFile[reference].Name;
                if (!Regex.IsMatch(text, $@"\b{Regex.Escape(name)}(?![\w-])"))
                {
                    unused.Add($"{project.File}: {name}");
                }
            }
        }

        string.Join(Environment.NewLine, unused).Should().BeEmpty(
            "a project whose namespace the referencing code never names is a reference " +
            "nothing uses: remove it");
    }

    /// <summary>
    /// A central version that no project references is listed with its reason, and
    /// nothing is listed that a project references or the file no longer holds.
    /// </summary>
    /// <remarks>Checked from both ends so the list cannot rot into a licence.</remarks>
    [Fact]
    public void CarryAReasonForEveryVersionNothingReferences()
    {
        var referenced = AllProjects.Value
            .SelectMany(project => project.Packages)
            .Concat(BuildPropsPackages())
            .ToHashSet(StringComparer.Ordinal);
        var unreferenced = CentralVersions().Where(package => !referenced.Contains(package)).ToArray();
        var listed = Unreferenced.Select(kept => kept.Package).ToHashSet(StringComparer.Ordinal);

        string.Join(", ", unreferenced.Where(package => !listed.Contains(package))).Should().BeEmpty(
            "a version nothing references is a pin or a leftover, and only a written reason " +
            "tells which: add it to Unreferenced with the reason, or remove the line");
        string.Join(", ", listed.Where(package => !unreferenced.Contains(package))).Should().BeEmpty(
            "the version is referenced now or gone from Directory.Packages.props, so the " +
            "reason is spent: drop the entry");
    }

    [Fact]
    public void KeepNoHintForAPackageNobodyReferences()
    {
        var product = ProductProjects().SelectMany(project => project.Packages).ToHashSet(StringComparer.Ordinal);
        var anywhere = AllProjects.Value.SelectMany(project => project.Packages)
            .Concat(BuildPropsPackages())
            .ToHashSet(StringComparer.Ordinal);

        Hints.Select(hint => hint.Package).Where(package => !product.Contains(package)).Should().BeEmpty(
            "no product project references the package any more: drop its hint");
        Tooling.Select(kept => kept.Package).Where(package => !anywhere.Contains(package)).Should().BeEmpty(
            "no project references the tool any more: drop the entry");
        Unreferenced.Concat(Tooling).Where(kept => string.IsNullOrWhiteSpace(kept.Why)).Should().BeEmpty(
            "an entry without a reason is a licence");
    }

    private static IEnumerable<Project> ProductProjects() =>
        AllProjects.Value.Where(project => project.File.StartsWith("src/", StringComparison.Ordinal));

    private static string Text(Project project, bool withJson) => string.Join('\n', RepositoryFiles.CommittableFiles()
        .Select(path => (Path: path, Relative: HandwrittenDocuments.Relative(path)))
        .Where(file => file.Relative.StartsWith(project.Directory + "/", StringComparison.Ordinal))
        .Where(file => EvidenceExtensions.Contains(Path.GetExtension(file.Path), StringComparer.OrdinalIgnoreCase))
        .Where(file => withJson || !file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        .Select(file => File.ReadAllText(file.Path)));

    private static IReadOnlyList<Project> ReadProjects() => RepositoryFiles.CommittableFiles()
        .Select(HandwrittenDocuments.Relative)
        .Where(file => file.EndsWith(".csproj", StringComparison.Ordinal))
        .Where(file => file.StartsWith("src/", StringComparison.Ordinal) || file.StartsWith("test/", StringComparison.Ordinal))
        .Select(ReadProject)
        .ToList();

    private static Project ReadProject(string file)
    {
        var directory = Path.GetDirectoryName(file)!.Replace('\\', '/');
        var xml = XDocument.Parse(RepositoryFiles.Read(file));
        var packages = Includes(xml, "PackageReference").ToArray();
        var projects = Includes(xml, "ProjectReference")
            .Select(include => HandwrittenDocuments.Relative(Path.GetFullPath(
                Path.Combine(DM.Testing.RepositoryLayout.Root, directory, include.Replace('\\', '/')))))
            .ToArray();
        return new Project(file, directory, Path.GetFileNameWithoutExtension(file), packages, projects);
    }

    private static IEnumerable<string> BuildPropsPackages() => RepositoryFiles.CommittableFiles()
        .Where(path => Path.GetFileName(path) == "Directory.Build.props")
        .SelectMany(path => Includes(XDocument.Load(path), "PackageReference"));

    private static IEnumerable<string> CentralVersions() =>
        Includes(XDocument.Parse(RepositoryFiles.Read("Directory.Packages.props")), "PackageVersion");

    private static IEnumerable<string> Includes(XDocument xml, string element) => xml
        .Descendants(element)
        .Select(item => (string?)item.Attribute("Include"))
        .OfType<string>();
}
