using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// Service for updating conversation messages
/// </summary>
public interface IMessageUpdatingService
{
    /// <summary>
    /// Update existing message
    /// </summary>
    /// <param name="updateMessage">Update message model</param>
    /// <returns></returns>
    Task<Message> Update(UpdateMessage updateMessage);
}
