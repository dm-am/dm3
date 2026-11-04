using System.Collections.Generic;
using DM.Domain.Core.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Shared.BotTrap;

/// <summary>
/// Answers 400 to a form whose trap field was filled, before its action runs.
/// </summary>
/// <remarks>
/// One filter over whatever form the action binds, instead of the same check at
/// the top of every action that takes one. Thrown, so ErrorHandlingMiddleware
/// shapes the body as it shapes every other refusal.
/// </remarks>
internal class HoneypotFilter : IActionFilter
{
    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is IHoneypotForm form && !string.IsNullOrWhiteSpace(form.Website))
            {
                var (field, sentence) = form.Refusal;
                throw new HttpBadRequestException(
                    new Dictionary<string, string> { [field] = sentence },
                    RefusalMessage.InvalidData);
            }
        }
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
