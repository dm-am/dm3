using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.BusinessProcesses.Topics.Creating;

/// <summary>
/// Creating topics storage
/// </summary>
internal interface ITopicCreatingRepository
{
    /// <summary>
    /// Create new topic
    /// </summary>
    /// <param name="forumTopic">DAL model</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>DTO model of created topic</returns>
    Task<Topic> Create(ForumTopic forumTopic, CancellationToken ct = default);
}