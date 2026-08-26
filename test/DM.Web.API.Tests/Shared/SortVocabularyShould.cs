using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Testing;
using DM.Web.API.Features.Forum.Topics;
using DM.Web.API.Shared.Sorting;
using AwesomeAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A sort field the endpoint does not have is an error, not a different order.
/// </summary>
/// <remarks>
/// API_DESIGN has said so since the sorting section was written; the code said
/// it on one endpoint out of thirteen. Everywhere else the repository's sort
/// switch ended in a default arm, so `?sortBy=nonsense` answered 200 with rows
/// in whatever order the endpoint falls back to — a wrong answer that looks
/// exactly like a right one.
/// </remarks>
public class SortVocabularyShould : UnitTestBase
{
    private readonly SortVocabularyFilter _filter = new();

    [Theory]
    [InlineData("nonsense")]
    [InlineData("createdUtc")] // the response field name, the tempting guess
    public async Task RefuseASortFieldTheEndpointDoesNotHave(string sortBy)
    {
        var ran = false;

        var act = () => _filter.OnActionExecutionAsync(
            ContextFor(new TopicsQuery { SortBy = sortBy }),
            () =>
            {
                ran = true;
                return Task.FromResult<ActionExecutedContext>(null!);
            });

        var failure = await act.Should().ThrowAsync<ValidationException>();
        failure.Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("SortBy");
        ran.Should().BeFalse("the action must not run on a refused query");
    }

    [Fact]
    public async Task RefuseADirectionThatIsNeitherAscNorDesc()
    {
        var act = () => _filter.OnActionExecutionAsync(
            ContextFor(new TopicsQuery { SortOrder = "ascending" }),
            () => Task.FromResult<ActionExecutedContext>(null!));

        var failure = await act.Should().ThrowAsync<ValidationException>();
        failure.Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("SortOrder");
    }

    [Theory]
    [InlineData("lastActivity", "desc")]
    [InlineData("LASTACTIVITY", "DESC")] // the repositories lowercase before switching
    [InlineData("likes", null)]
    [InlineData(null, null)]
    [InlineData("", "")]
    public async Task LetThroughWhatTheEndpointSorts(string? sortBy, string? sortOrder)
    {
        var ran = false;

        await _filter.OnActionExecutionAsync(
            ContextFor(new TopicsQuery
            {
                SortBy = sortBy,
                SortOrder = sortOrder
            }),
            () =>
            {
                ran = true;
                return Task.FromResult<ActionExecutedContext>(null!);
            });

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveAQueryWithoutASortAlone()
    {
        var ran = false;

        await _filter.OnActionExecutionAsync(
            ContextFor(new DM.Domain.Core.Dto.PagingQuery()),
            () =>
            {
                ran = true;
                return Task.FromResult<ActionExecutedContext>(null!);
            });

        ran.Should().BeTrue();
    }

    /// <summary>
    /// The vocabulary is keyed by the bound type, so a list endpoint added with
    /// a SortBy of its own and no entry here would go back to answering 200 to
    /// anything. Reflection over the controllers is what notices.
    /// </summary>
    [Fact]
    public void NameTheFieldsOfEverySortableQueryTheApiBinds()
    {
        var unregistered = new List<string>();
        var checkedTypes = 0;

        var controllers = typeof(Startup).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)));

        foreach (var controller in controllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.DeclaredOnly))
            {
                foreach (var parameter in action.GetParameters())
                {
                    var type = parameter.ParameterType;
                    var sortBy = type.GetProperty("SortBy", BindingFlags.Public | BindingFlags.Instance);
                    if (sortBy == null)
                    {
                        continue;
                    }

                    checkedTypes++;

                    // A sortBy the caller spells as a string is only bounded by
                    // the vocabulary — the filter reads string properties and
                    // nothing else, so an unregistered one accepts anything.
                    // Named by an enum, model binding refuses the unknown value
                    // before any of this runs, and the entry is there to say so
                    // rather than to hold a list: /v1/users names its field with
                    // UserSort, and calls it sortBy like the thirteen lists
                    // beside it.
                    var required = (Nullable.GetUnderlyingType(sortBy.PropertyType) ?? sortBy.PropertyType).IsEnum
                        ? SortVocabulary.FieldsOf(type) is not null
                        : SortVocabulary.FieldsOf(type) is { Count: > 0 };

                    if (!required)
                    {
                        unregistered.Add($"{controller.Name}.{action.Name}: {type.Name}");
                    }
                }
            }
        }

        checkedTypes.Should().BeGreaterThan(10, "the API binds sortable list queries");
        unregistered.Should().BeEmpty(
            "a query with a sortBy and no vocabulary accepts any value and sorts by its default");
    }

    private static ActionExecutingContext ContextFor(object query) =>
        new(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?> { ["q"] = query },
            controller: null!);
}
