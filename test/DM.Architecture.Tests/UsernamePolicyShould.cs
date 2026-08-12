using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DM.Domain.Core.Users;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// What a username may be is decided in one place.
/// </summary>
/// <remarks>
/// It was decided in six, under two models that answer differently. Three domain
/// validators and the client said what a name may not contain; two request DTOs
/// said what it may. Because [ApiController] runs model validation before the
/// action, the DTO won on submission while the domain won on the availability
/// check — so the site told a person their name was free and then refused it.
///
/// Read by reflection rather than by grepping the sources: this suite already
/// carries one finding about text checks passing over commented-out code, and an
/// attribute either is on a property or is not.
/// </remarks>
public class UsernamePolicyShould
{
    private static readonly Assembly Api = typeof(DM.Web.API.Startup).Assembly;

    [Fact]
    public void BeTheOnlyThingThatDecidesWhichCharactersAUsernameMayHold()
    {
        var offenders = Api.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name == "Username")
                .Where(property => property.GetCustomAttribute<RegularExpressionAttribute>() != null)
                .Select(property => $"{type.Name}.{property.Name}"))
            .OrderBy(offender => offender, StringComparer.Ordinal)
            .ToArray();

        offenders.Should().BeEmpty(
            "a pattern on the request DTO is a second answer to a question the domain " +
            "validator already answers, and the two disagreed about real names");
    }

    [Fact]
    public void KeepTheBoundsTheSchemaPublishes()
    {
        // Dropping the pattern is not dropping the constraint: length and presence
        // are simple limits, they belong on the DTO by convention, and they are
        // what a generated client has left to go on.
        //
        // Only the requests where a name is being CHOSEN. A DTO that names an
        // existing user — blocking one, inviting one — is not stating what a name
        // may be, and holding it to these bounds would be the same mistake in the
        // other direction.
        var chosen = new[] { "ActivationRequest", "UsernameChangeCompletionRequest" };
        var requests = Api.GetTypes()
            .Where(type => chosen.Contains(type.Name))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name == "Username")
                .Where(property => property.GetCustomAttribute<RequiredAttribute>() != null)
                .Select(property => (type.Name, Property: property)))
            .ToArray();

        requests.Should().HaveCount(chosen.Length,
            "both requests that carry a chosen name are what this is about");
        foreach (var (owner, property) in requests)
        {
            property.GetCustomAttribute<StringLengthAttribute>()
                .Should().NotBeNull($"{owner} publishes the length a username may have");
        }
    }

    [Fact]
    public void AcceptAndRefuseTheNamesTheDocumentDescribes()
    {
        var policy = new Regex(UsernamePolicy.Pattern);

        policy.IsMatch("JohnDoe_123").Should().BeTrue();
        policy.IsMatch("Алена").Should().BeTrue("a Cyrillic name is an ordinary name here");
        policy.IsMatch("Мастер Игры").Should().BeTrue("one inner space is allowed");

        policy.IsMatch("a").Should().BeFalse("two characters is the floor");
        policy.IsMatch(new string('a', 21)).Should().BeFalse("twenty is the ceiling");
        policy.IsMatch(" leading").Should().BeFalse();
        policy.IsMatch("trailing ").Should().BeFalse();
        policy.IsMatch("two  spaces").Should().BeFalse();
        policy.IsMatch("<script>").Should().BeFalse("HTML-unsafe characters are refused");
        policy.IsMatch("zero​width").Should().BeFalse("a zero-width character is refused");

        // A name is a path segment, and every HTTP client resolves dot-segments
        // away before the request leaves: /users/.. is /users/, so an account
        // called ".." has no address. The allow-list this replaced happened to
        // refuse these by demanding an alphanumeric first and last character.
        policy.IsMatch("..").Should().BeFalse();
        policy.IsMatch("...").Should().BeFalse();
        policy.IsMatch("-.-").Should().BeFalse();
        policy.IsMatch("___").Should().BeFalse("a name with nothing to read is not a name");
        policy.IsMatch("_x_").Should().BeTrue("one letter among the punctuation is enough");
    }
}
