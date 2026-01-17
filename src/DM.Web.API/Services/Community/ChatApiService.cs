using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Chat.Creating;
using DM.Services.Community.BusinessProcesses.Chat.Deleting;
using DM.Services.Community.BusinessProcesses.Chat.Likes;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Community.BusinessProcesses.Chat.Updating;
using DM.Services.Core.Dto;
using DM.Web.API.Dto.Contracts;
using ChatMessage = DM.Web.API.Dto.Messaging.ChatMessage;

namespace DM.Web.API.Services.Community;

/// <inheritdoc />
internal class ChatApiService : IChatApiService
{
    private readonly IChatReadingService _readingService;
    private readonly IChatCreatingService _creatingService;
    private readonly IChatUpdatingService _updatingService;
    private readonly IChatDeletingService _deletingService;
    private readonly IChatLikesService _likesService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatApiService(
        IChatReadingService readingService,
        IChatCreatingService creatingService,
        IChatUpdatingService updatingService,
        IChatDeletingService deletingService,
        IChatLikesService likesService,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _updatingService = updatingService;
        _deletingService = deletingService;
        _likesService = likesService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatMessage>> GetMessages(PagingQuery query)
    {
        var (messages, paging) = await _readingService.GetMessages(query);
        return new ListEnvelope<ChatMessage>(messages.Select(_mapper.Map<ChatMessage>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> CreateMessage(ChatMessage message)
    {
        var createMessage = _mapper.Map<CreateChatMessage>(message);
        var createdMessage = await _creatingService.Create(createMessage);
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(createdMessage));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> GetMessage(Guid id)
    {
        var message = await _readingService.GetMessage(id);
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> UpdateMessage(Guid id, ChatMessage message)
    {
        var updatedMessage = await _updatingService.Update(id, message.Text.Value);
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(updatedMessage));
    }

    /// <inheritdoc />
    public async Task DeleteMessage(Guid id)
    {
        await _deletingService.Delete(id);
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> LikeMessage(Guid id)
    {
        var message = await _likesService.Like(id);
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(message));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> UnlikeMessage(Guid id)
    {
        var message = await _likesService.Unlike(id);
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(message));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatMessage>> GetMessagesByDate(DateOnly date)
    {
        var messages = await _readingService.GetMessagesByDate(date);
        return new ListEnvelope<ChatMessage>(messages.Select(_mapper.Map<ChatMessage>));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatMessage>> GetMessagesBefore(Guid messageId, int count)
    {
        var (messages, hasMoreBefore) = await _readingService.GetMessagesBefore(messageId, count);
        return new ListEnvelope<ChatMessage>(
            messages.Select(_mapper.Map<ChatMessage>),
            new Paging(hasMoreBefore, false));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatMessage>> GetMessagesAfter(Guid messageId, int count)
    {
        var (messages, hasMoreAfter) = await _readingService.GetMessagesAfter(messageId, count);
        return new ListEnvelope<ChatMessage>(
            messages.Select(_mapper.Map<ChatMessage>),
            new Paging(false, hasMoreAfter));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatMessage>> GetMessagesAround(Guid messageId, int count)
    {
        var (messages, hasMoreBefore, hasMoreAfter) = await _readingService.GetMessagesAround(messageId, count);
        return new ListEnvelope<ChatMessage>(
            messages.Select(_mapper.Map<ChatMessage>),
            new Paging(hasMoreBefore, hasMoreAfter));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatMessage>> GetFirstMessageOnOrAfterDate(DateOnly date)
    {
        var message = await _readingService.GetFirstMessageOnOrAfterDate(date);
        if (message == null)
        {
            throw new DM.Services.Core.Exceptions.HttpException(
                System.Net.HttpStatusCode.NotFound, "No messages found on or after this date");
        }
        return new Envelope<ChatMessage>(_mapper.Map<ChatMessage>(message));
    }
}
