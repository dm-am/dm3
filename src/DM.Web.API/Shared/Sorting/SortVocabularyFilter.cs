using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Shared.Sorting;

/// <summary>
/// Answers 400 to a sort field the endpoint does not have.
/// </summary>
/// <remarks>
/// The alternative was a validator per list query, eleven of them, each
/// repeating the same rule and each needing its service to call
/// ValidateAndThrowAsync — which is how the rule came to exist on exactly one
/// endpoint. This runs before the action instead, over whatever query object
/// the action binds, and takes its answer from SortVocabulary.
///
/// The failure is a FluentValidation ValidationException so that it comes out
/// of ErrorHandlingMiddleware in the same problem+json shape, under the same
/// "SortBy"/"SortOrder" keys, as the one endpoint that already refused —
/// nothing downstream has to learn a second error format.
/// </remarks>
internal class SortVocabularyFilter : IAsyncActionFilter
{
    private const string SortByProperty = "SortBy";
    private const string SortOrderProperty = "SortOrder";

    /// <inheritdoc />
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null)
            {
                continue;
            }

            var fields = SortVocabulary.FieldsOf(argument.GetType());
            if (fields == null)
            {
                continue;
            }

            // An empty vocabulary means the field is named by an enum of the
            // type's own, which model binding has already refused if unknown.
            if (fields.Count > 0)
            {
                Check(argument, SortByProperty, fields,
                    $"SortBy must be one of: {string.Join(", ", fields)}", failures);
            }

            Check(argument, SortOrderProperty, SortVocabulary.SortOrders,
                "SortOrder must be 'asc' or 'desc'", failures);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return next();
    }

    /// <summary>
    /// An absent sort is not a wrong sort: only a value that was sent and is
    /// not in the vocabulary fails.
    /// </summary>
    private static void Check(
        object query,
        string propertyName,
        IReadOnlyList<string> allowed,
        string message,
        List<ValidationFailure> failures)
    {
        var property = query.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetValue(query) is not string value || string.IsNullOrEmpty(value))
        {
            return;
        }

        if (!SortVocabulary.Allows(allowed, value))
        {
            failures.Add(new ValidationFailure(propertyName, message) { AttemptedValue = value });
        }
    }
}
