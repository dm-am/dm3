using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using DM.Domain.Core.Extensions;
using DM.Web.API.Shared.Binding;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace DM.Web.API.Tests.Shared.Binding;

/// <summary>
/// An identifier in a route or a query binds from either of the two spellings
/// the site hands out, and nothing else binds.
/// </summary>
/// <remarks>Every Guid parameter goes through this binder, and the site's links carry either spelling.</remarks>
public class ReadableGuidBinderShould
{
    private static readonly Guid Identifier = Guid.Parse("8b1e4c2a-37d5-4f61-9a0b-c3d4e5f60718");

    public static TheoryData<string> Spellings => new()
    {
        Identifier.ToString(),
        Identifier.ToString("N"),
        Identifier.EncodeToReadable(),
        Identifier.EncodeToReadable("Тема о драконах"),
    };

    [Theory]
    [MemberData(nameof(Spellings))]
    public async Task BindEverySpellingTheSiteHandsOut(string spelling)
    {
        var bound = await Bind(typeof(Guid), spelling);

        bound.Result.IsModelSet.Should().BeTrue();
        bound.Result.Model.Should().Be(Identifier);
        bound.ModelState.ErrorCount.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public async Task BindEverySpellingIntoAnOptionalIdentifierToo(string spelling)
    {
        var bound = await Bind(typeof(Guid?), spelling);

        bound.Result.IsModelSet.Should().BeTrue();
        bound.Result.Model.Should().Be(Identifier);
    }

    public static TheoryData<Type, string> Garbage => new()
    {
        { typeof(Guid), "not-a-guid!" },
        { typeof(Guid), "gamea" },
        { typeof(Guid), "12345" },
        { typeof(Guid?), "not-a-guid!" },
        { typeof(Guid?), "gamea" },
        { typeof(Guid?), "12345" },
    };

    /// <summary>
    /// A value that is neither spelling is refused, an optional identifier included.
    /// </summary>
    /// <remarks>Read as "no identifier", a mistyped link would ask for nothing in particular; the body converter refuses alike.</remarks>
    [Theory]
    [MemberData(nameof(Garbage))]
    public async Task RefuseAValueThatIsNeitherSpelling(Type modelType, string value)
    {
        var bound = await Bind(modelType, value);

        bound.Result.IsModelSet.Should().BeFalse(
            "a value that decodes to nothing must not bind to the empty identifier and reach a lookup");
        bound.ModelState["id"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Invalid value for field id");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task AskForAValueARequiredIdentifierWasNotGiven(string? value)
    {
        var bound = await Bind(typeof(Guid), value);

        bound.Result.IsModelSet.Should().BeFalse();
        bound.ModelState["id"]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Field id should contain value");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task LeaveAnOptionalIdentifierEmptyWhenNoneWasGiven(string? value)
    {
        var bound = await Bind(typeof(Guid?), value);

        bound.Result.IsModelSet.Should().BeTrue("an optional identifier that was not given is a value of its own");
        bound.Result.Model.Should().BeNull();
        bound.ModelState.ErrorCount.Should().Be(0);
    }

    /// <summary>Binds a parameter named "id" from a query that carries the value, or none.</summary>
    private static async Task<DefaultModelBindingContext> Bind(Type modelType, string? value)
    {
        var query = value is null
            ? new Dictionary<string, StringValues>()
            : new Dictionary<string, StringValues> { ["id"] = value };

        var context = new DefaultModelBindingContext
        {
            ModelName = "id",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType),
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query, new QueryCollection(query), CultureInfo.InvariantCulture),
        };

        await new ReadableGuidBinder().BindModelAsync(context);
        return context;
    }
}
