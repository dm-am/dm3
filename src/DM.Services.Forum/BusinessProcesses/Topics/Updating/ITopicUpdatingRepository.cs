using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using TopicDto = DM.Services.Forum.Dto.Output.Topic;

namespace DM.Services.Forum.BusinessProcesses.Topics.Updating;

/// <summary>
/// Updating topics storage
/// </summary>
internal interface ITopicUpdatingRepository
{
    /// <summary>
    /// Update existing topic
    /// </summary>
    /// <param name="updateBuilder"></param>
    /// <returns>DTO model of updated topic</returns>
    Task<TopicDto> Update(IUpdateBuilder<TopicDal> updateBuilder);
}