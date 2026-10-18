using DM.Domain.Moderation.Features.Tickets;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;
using DomainTicketComment = DM.Domain.Moderation.Features.Tickets.TicketComment;
using DomainTicketDetails = DM.Domain.Moderation.Features.Tickets.TicketDetails;

namespace DM.Web.API.Features.Moderation.Tickets;

/// <summary>
/// Compile-time mapper for moderation tickets. The body goes out twice: as
/// stored, and in a text carrier the serializer renders; a comment of the
/// conversation goes out rendered.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class TicketMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public TicketMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain ticket to the DTO of the lists. TicketId becomes Id - otherwise Id
    /// stays Guid.Empty and clients cannot address the ticket.
    /// </summary>
    [MapProperty(nameof(DomainTicket.TicketId), nameof(Ticket.Id))]
    [MapProperty(nameof(DomainTicket.Description), nameof(Ticket.DescriptionHtml))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Ticket ToTicket(DomainTicket ticket);

    /// <summary>
    /// Domain ticket with its conversation to the DTO of the page of a ticket
    /// </summary>
    [MapProperty(nameof(DomainTicketDetails.TicketId), nameof(TicketDetails.Id))]
    [MapProperty(nameof(DomainTicketDetails.Description), nameof(TicketDetails.DescriptionHtml))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial TicketDetails ToTicketDetails(DomainTicketDetails ticket);

    /// <summary>
    /// Domain comment of a conversation to its DTO, the text rendered with the
    /// tag set of comments
    /// </summary>
    [MapProperty(nameof(DomainTicketComment.TicketCommentId), nameof(TicketComment.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial TicketComment ToTicketComment(DomainTicketComment comment);

    /// <summary>
    /// Create request to the domain command
    /// </summary>
    public partial CreateTicket ToCreateTicket(CreateTicketRequest request);

    /// <summary>
    /// Comment request of the guest to the domain command. The guest is the
    /// author of his ticket, and the author chooses no status
    /// </summary>
    [MapperIgnoreTarget(nameof(CreateTicketComment.Status))]
    public partial CreateTicketComment ToCreateTicketComment(CreateTicketCommentRequest request);

    /// <summary>
    /// Answer request to the domain command
    /// </summary>
    public partial CreateTicketComment ToCreateTicketComment(AnswerTicketRequest request);
}
