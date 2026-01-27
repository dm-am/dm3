using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DbMessage = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Chat.Creating;

/// <summary>
/// Storage for chat creating
/// </summary>
internal interface IChatCreatingRepository
{
    /// <summary>
    /// Create new chat message
    /// </summary>
    /// <param name="message">DAL model</param>
    /// <returns></returns>
    Task<ChatMessage> Create(DbMessage message);
}
