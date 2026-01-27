using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Messaging.Creating;
using DM.Services.Community.BusinessProcesses.Messaging.Deleting;
using DM.Services.Community.BusinessProcesses.Messaging.Likes;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Community.BusinessProcesses.Messaging.Updating;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Contracts;
using Conversation = DM.Web.API.Dto.Messaging.Conversation;
using CreateConversation = DM.Web.API.Dto.Messaging.CreateConversation;
using UpdateConversation = DM.Web.API.Dto.Messaging.UpdateConversation;
using Message = DM.Web.API.Dto.Messaging.Message;
using ServiceCreateConversation = DM.Services.Community.BusinessProcesses.Messaging.Creating.CreateConversation;
using ServiceUpdateConversation = DM.Services.Community.BusinessProcesses.Messaging.Updating.UpdateConversation;

namespace DM.Web.API.Services.Community;

/// <inheritdoc />
internal class MessagingApiService : IMessagingApiService
{
    private readonly IConversationReadingService conversationReadingService;
    private readonly IConversationCreatingService conversationCreatingService;
    private readonly IConversationUpdatingService conversationUpdatingService;
    private readonly IMessageReadingService messageReadingService;
    private readonly IMessageCreatingService messageCreatingService;
    private readonly IMessageUpdatingService messageUpdatingService;
    private readonly IMessageDeletingService messageDeletingService;
    private readonly IMessageLikeService messageLikeService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public MessagingApiService(
        IConversationReadingService conversationReadingService,
        IConversationCreatingService conversationCreatingService,
        IConversationUpdatingService conversationUpdatingService,
        IMessageReadingService messageReadingService,
        IMessageCreatingService messageCreatingService,
        IMessageUpdatingService messageUpdatingService,
        IMessageDeletingService messageDeletingService,
        IMessageLikeService messageLikeService,
        IMapper mapper)
    {
        this.conversationReadingService = conversationReadingService;
        this.conversationCreatingService = conversationCreatingService;
        this.conversationUpdatingService = conversationUpdatingService;
        this.messageReadingService = messageReadingService;
        this.messageCreatingService = messageCreatingService;
        this.messageUpdatingService = messageUpdatingService;
        this.messageDeletingService = messageDeletingService;
        this.messageLikeService = messageLikeService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Conversation>> GetConversations(PagingQuery query)
    {
        var (conversations, paging) = await conversationReadingService.Get(query);
        return new ListEnvelope<Conversation>(conversations.Select(mapper.Map<Conversation>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Conversation>> GetConversation(Guid id)
    {
        var conversation = await conversationReadingService.Get(id);
        return new Envelope<Conversation>(mapper.Map<Conversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<Envelope<Conversation>> GetConversation(string login)
    {
        var conversation = await conversationReadingService.GetOrCreate(login);
        return new Envelope<Conversation>(mapper.Map<Conversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<Envelope<Conversation>> GetDirectConversation(Guid visaviUserId)
    {
        var conversation = await conversationReadingService.GetOrCreate(visaviUserId);
        return new Envelope<Conversation>(mapper.Map<Conversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Message>> GetMessages(Guid conversationId, PagingQuery query)
    {
        var (messages, paging) = await messageReadingService.Get(conversationId, query);
        return new ListEnvelope<Message>(messages.Select(mapper.Map<Message>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Message>> CreateMessage(Guid conversationId, Message message)
    {
        var createMessage = mapper.Map<CreateMessage>(message);
        createMessage.ConversationId = conversationId;
        var createdMessage = await messageCreatingService.Create(createMessage);
        return new Envelope<Message>(mapper.Map<Message>(createdMessage));
    }

    /// <inheritdoc />
    public async Task<Envelope<Message>> GetMessage(Guid messageId)
    {
        var message = await messageReadingService.Get(messageId);
        return new Envelope<Message>(mapper.Map<Message>(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<Message>> UpdateMessage(Guid messageId, Message message)
    {
        var updateMessage = mapper.Map<UpdateMessage>(message);
        updateMessage.MessageId = messageId;
        var updatedMessage = await messageUpdatingService.Update(updateMessage);
        return new Envelope<Message>(mapper.Map<Message>(updatedMessage));
    }

    /// <inheritdoc />
    public Task DeleteMessage(Guid messageId) => messageDeletingService.Delete(messageId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid conversationId) => conversationReadingService.MarkAsRead(conversationId);

    /// <inheritdoc />
    public async Task<Envelope<Message>> LikeMessage(Guid messageId)
    {
        await messageLikeService.LikeMessage(messageId);
        return await GetMessage(messageId);
    }

    /// <inheritdoc />
    public Task UnlikeMessage(Guid messageId) => messageLikeService.DislikeMessage(messageId);

    /// <inheritdoc />
    public async Task<Envelope<Conversation>> CreateConversation(CreateConversation createConversation)
    {
        var serviceCreateConversation = mapper.Map<ServiceCreateConversation>(createConversation);
        var conversation = await conversationCreatingService.CreateGroup(serviceCreateConversation);
        return new Envelope<Conversation>(mapper.Map<Conversation>(conversation));
    }

    /// <inheritdoc />
    public async Task<Envelope<Conversation>> UpdateConversation(Guid conversationId, UpdateConversation updateConversation)
    {
        var serviceUpdateConversation = mapper.Map<ServiceUpdateConversation>(updateConversation);
        serviceUpdateConversation.ConversationId = conversationId;
        var conversation = await conversationUpdatingService.Update(serviceUpdateConversation);
        return new Envelope<Conversation>(mapper.Map<Conversation>(conversation));
    }
}