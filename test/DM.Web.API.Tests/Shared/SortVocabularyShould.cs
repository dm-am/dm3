using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
/// <remarks>A default arm in a sort switch answers 200 in a fallback order.</remarks>
public class SortVocabularyShould
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
    /// The vocabulary is keyed by the bound type, so every list endpoint's SortBy needs an entry.
    /// </summary>
    [Fact]
    public void NameTheFieldsOfEverySortableQueryTheApiBinds()
    {
        var unregistered = new List<string>();
        var checkedTypes = 0;

        foreach (var action in ApiSurface.Actions())
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

                // A string sortBy is bounded only by the vocabulary; an enum one by model binding.
                var required = (Nullable.GetUnderlyingType(sortBy.PropertyType) ?? sortBy.PropertyType).IsEnum
                    ? SortVocabulary.FieldsOf(type) is not null
                    : SortVocabulary.FieldsOf(type) is { Count: > 0 };

                if (!required)
                {
                    unregistered.Add($"{ApiSurface.Named(action)}: {type.Name}");
                }
            }
        }

        checkedTypes.Should().BePositive("the API binds sortable list queries");
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
