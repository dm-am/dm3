using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.DataAccess.RelationalStorage;
using MessageDal = DM.Services.DataAccess.BusinessObjects.Messaging.Message;

namespace DM.Services.Community.BusinessProcesses.Messaging.Updating;

/// <summary>
/// Updating message storage
/// </summary>
internal interface IMessageUpdatingRepository
{
    /// <summary>
    /// Update single message
    /// </summary>
    /// <param name="update">Update message</param>
    /// <returns></returns>
    Task<Message> Update(IUpdateBuilder<MessageDal> update);
}
