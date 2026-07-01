using System;
using System.Threading.Tasks;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// Прямой WebSocket-push о смене аватара пользователя (transient UI hint).
/// Не идет через outbox/RabbitMQ — событие best-effort, потеря пуша
/// несущественна (FE подхватит изменение при следующем page-load).
///
/// Аналогично presence-updates и typing-indicators: durability не нужна,
/// важна низкая латентность доставки в открытые вкладки.
///
/// Payload содержит только userId — клиент сам перезагружает свои данные,
/// чужие аватары в DOM обновляются на следующем рендере с новыми URL
/// (immutable hash-based keys → browser cache safe).
/// </summary>
public interface IRealtimeAvatarBroadcaster
{
    /// <summary>
    /// Broadcast информацию о смене аватара. Если соответствующих
    /// подключений нет — no-op.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя, чей аватар изменился.</param>
    Task BroadcastAvatarChangedAsync(Guid userId);
}
