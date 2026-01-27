using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// Service for updating conversations
/// </summary>
public interface IConversationUpdatingService
{
    /// <summary>
    /// Update conversation (title and/or participants)
    /// </summary>
    /// <param name="updateConversation">Update data</param>
    /// <returns>Updated conversation</returns>
    Task<Conversation> Update(UpdateConversation updateConversation);
}
