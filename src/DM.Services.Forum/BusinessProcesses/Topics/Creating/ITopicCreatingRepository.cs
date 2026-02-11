using System.Threading;
using System.Threading.Tasks;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;

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
    Task<TopicDto> Create(TopicDal forumTopic, CancellationToken ct = default);
}