using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <summary>
/// Service for creating conversations
/// </summary>
public interface IConversationCreatingService
{
    /// <summary>
    /// Create a new group conversation
    /// </summary>
    /// <param name="createConversation">Conversation data</param>
    /// <returns>Created conversation</returns>
    Task<Conversation> CreateGroup(CreateConversation createConversation);
}
