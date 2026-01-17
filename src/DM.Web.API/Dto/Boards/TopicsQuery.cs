using DM.Services.Core.Dto;

namespace DM.Web.API.Dto.Boards;

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