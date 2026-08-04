using System;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A field the client sends has to be the field the server binds.
/// </summary>
/// <remarks>
/// The activation body carried "expectedEmail" and the DTO declared
/// "RetryEmail". No JsonPropertyName stands between them and the serializer is
/// camelCase, so the value arrived null on every request — and the branch it
/// feeds is the idempotent-retry guard, which therefore answered 410 to a
/// repeat of an activation that had already succeeded, sending the reader off
/// into the password-recovery flow for an account that already existed.
///
/// Nothing failed over it. The service test for that branch builds the request
/// object in C# and never crosses the wire, so it stayed green with the wiring
/// broken; the controller's own XML doc had drifted to a third spelling of the
/// field, which is what a reader would have checked against.
///
/// Textual, and by file name, because that is where the pair lives: a typed
/// check would have to compile the client's TypeScript.
/// </remarks>
public class ClientRequestFieldsShould
{
    [Fact]
    public void SpellTheActivationRetryEmailTheWayTheDtoBindsIt()
    {
        var root = RepositoryRoot;
        var dto = File.ReadAllText(Path.Combine(root, "src", "DM.Web.API", "Features",
            "Account", "Registration", "ActivationRequest.cs"));
        var client = File.ReadAllText(Path.Combine(root, "src", "DM.Web.Client", "src",
            "entities", "user", "api", "accountApi.ts"));

        var declaration = Regex.Match(dto, @"public string\?\s+(\w+)\s*\{ get; set; \}");
        declaration.Success.Should().BeTrue(
            "ActivationRequest declares the optional retry email");

        var property = declaration.Groups[1].Value;
        var wireName = char.ToLowerInvariant(property[0]) + property[1..];

        client.Should().Contain(wireName,
            $"the activation body key is the DTO property camelCased ({wireName}), " +
            "and a mismatch reaches the server as a silent null");
        dto.Should().NotContain("JsonPropertyName",
            "the pair holds by one spelling, not by a rename attribute nobody reads");
    }

    /// <summary>
    /// Walks up from the test binary to the repository root: the sources are not
    /// copied to the output directory, and copying them would assert against a
    /// stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
