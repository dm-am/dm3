using System.Threading.Tasks;
using DM.Web.API.Dto.Messaging;

namespace DM.Web.API.Services.Community;

/// <summary>
/// Hub service for global chat (real-time messaging)
/// </summary>
internal interface IGlobalChatHubClient
{
    /// <summary>
    /// Send new message to all connected users
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <returns></returns>
    Task SendMessage(Message message);
}
