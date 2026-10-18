using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Filter and page for the warning log, the open one and the moderation one alike.
/// </summary>
/// <remarks>
/// One class for both endpoints, because they narrow and page the same list and
/// differ only in who may call them and in what a row carries.
/// </remarks>
public class WarningsQuery : PagingQuery
{
    /// <summary>
    /// Username the log is narrowed to. Empty is the whole website; a name nobody
    /// carries is an empty page rather than a refusal.
    /// </summary>
    /// <example>problemuser</example>
    public string? Username { get; set; }
}
