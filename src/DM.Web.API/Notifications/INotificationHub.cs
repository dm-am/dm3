using System.Threading.Tasks;
using DM.Web.API.Features.Personal.Notifications;

namespace DM.Web.API.Notifications;

/// <summary>
/// Realtime notifications SignalR hub
/// </summary>
public interface INotificationHub
{
    /// <summary>
    /// Send notification
    /// </summary>
    /// <param name="notification"></param>
    Task Send(Notification notification);
}