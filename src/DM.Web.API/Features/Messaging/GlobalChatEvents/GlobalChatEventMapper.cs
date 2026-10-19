using System.Linq;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using ServiceCreateGlobalChatEvent = DM.Domain.Messaging.Features.GlobalChatEvents.CreateGlobalChatEvent;
using ServiceGlobalChatEvent = DM.Domain.Messaging.Features.GlobalChatEvents.GlobalChatEvent;
using ServiceUpdateGlobalChatEvent = DM.Domain.Messaging.Features.GlobalChatEvents.UpdateGlobalChatEvent;

namespace DM.Web.API.Features.Messaging.GlobalChatEvents;

/// <summary>
/// Compile-time mapper for global chat events
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class GlobalChatEventMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public GlobalChatEventMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain chat event to the full DTO. No envelope on Description: nothing reads
    /// AuthorEdit on the event endpoints, and a stray author_edit header falls back to Display.
    /// </summary>
    public partial GlobalChatEvent ToEvent(ServiceGlobalChatEvent chatEvent);

    /// <summary>
    /// Domain chat event to the list summary. A narrowing projection: the
    /// participant list collapses into a count.
    /// </summary>
    public GlobalChatEventSummary ToSummary(ServiceGlobalChatEvent chatEvent)
    {
        var summary = ToSummaryCore(chatEvent);
        summary.ParticipantCount = chatEvent.Participants != null
            ? chatEvent.Participants.Count()
            : 0;
        return summary;
    }

    [MapperIgnoreTarget(nameof(GlobalChatEventSummary.ParticipantCount))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GlobalChatEventSummary ToSummaryCore(ServiceGlobalChatEvent chatEvent);

    /// <summary>
    /// Create input to the domain create command
    /// </summary>
    public partial ServiceCreateGlobalChatEvent ToCreateEvent(CreateGlobalChatEventInput input);

    /// <summary>
    /// Update input to the domain update command. Id comes from the route.
    /// </summary>
    [MapperIgnoreTarget(nameof(ServiceUpdateGlobalChatEvent.Id))]
    public partial ServiceUpdateGlobalChatEvent ToUpdateEvent(UpdateGlobalChatEventInput input);
}
