using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// The bounds of the page size of a list that takes one as a parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class PageSizeAttribute : RangeAttribute
{
    public PageSizeAttribute() : base(1, 100)
    {
        ErrorMessage = "Размер страницы должен быть от 1 до 100";
    }
}
