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
///
/// Comments are stripped from the client before the name is looked for, and both
/// of the client's files are read. The first version of this test did neither:
/// it searched the whole text of one file, so the paragraph above the signature
/// — which names the field while explaining the bug — answered for the code, and
/// the literal that actually goes on the wire is assembled in the page, which the
/// test never opened. Restoring the original defect in both places left it green.
/// </remarks>
public class ClientRequestFieldsShould
{
    [Fact]
    public void SpellTheActivationRetryEmailTheWayTheDtoBindsIt()
    {
        var root = RepositoryRoot;
        var dto = File.ReadAllText(Path.Combine(root, "src", "DM.Web.API", "Features",
            "Account", "Registration", "ActivationRequest.cs"));

        var declaration = Regex.Match(dto, @"public string\?\s+(\w+)\s*\{ get; set; \}");
        declaration.Success.Should().BeTrue(
            "ActivationRequest declares the optional retry email");

        var property = declaration.Groups[1].Value;
        var wireName = char.ToLowerInvariant(property[0]) + property[1..];

        // The signature the request object is typed by, and the place the value is put into it.
        var senders = new[]
        {
            Path.Combine("entities", "user", "api", "accountApi.ts"),
            Path.Combine("pages", "account", "AccountActivationPage.vue"),
        };

        foreach (var sender in senders)
        {
            var path = Path.Combine(root, "src", "DM.Web.Client", "src", sender);
            File.Exists(path).Should().BeTrue($"{sender} is where the activation body is built");

            SourceText.ReadCode(path)
                .Should().Contain(wireName,
                    $"the activation body key is the DTO property camelCased ({wireName}), " +
                    $"and a mismatch reaches the server as a silent null; {sender} spells it " +
                    "outside of any comment or it does not spell it at all");
        }

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
