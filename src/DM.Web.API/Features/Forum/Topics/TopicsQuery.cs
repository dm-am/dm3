using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Forum.Topics;

/// <summary>
/// Input DTO for topics filtering
/// </summary>
public class TopicsQuery : PagingQuery
{
    /// <summary>
    /// Filter attached/non attached
    /// </summary>
    public bool IsAttached { get; set; }
}
