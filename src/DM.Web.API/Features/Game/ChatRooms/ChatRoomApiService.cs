using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Messaging.Features.Chats;
using DM.Web.API.Features.Messaging;
using DM.Web.API.Features.Messaging.Messages;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using CreateRoom = DM.Domain.Game.Features.Rooms.CreateRoom;
using UpdateRoom = DM.Domain.Game.Features.Rooms.UpdateRoom;

namespace DM.Web.API.Features.Game.ChatRooms;

/// <inheritdoc />
internal class ChatRoomApiService : IChatRoomApiService
{
    private readonly IRoomService _roomService;
    private readonly IRoomRepository _roomRepository;
    private readonly IChatService _chatService;
    private readonly IMessagingApiService _messagingService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatRoomApiService(
        IRoomService roomService,
        IRoomRepository roomRepository,
        IChatService chatService,
        IMessagingApiService messagingService,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IMapper mapper)
    {
        _roomService = roomService;
        _roomRepository = roomRepository;
        _chatService = chatService;
        _messagingService = messagingService;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<ChatRoom>> GetChatRoomsAsync(Guid gameId)
    {
        var rooms = await _roomService.GetAllAsync(gameId);
        var chatRooms = rooms
            .Where(r => r.Type == RoomType.Chat)
            .Select(_mapper.Map<ChatRoom>);
        return new ListEnvelope<ChatRoom>(chatRooms);
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatRoom>> GetChatRoomAsync(Guid id)
    {
        var room = await _roomService.GetAsync(id);
        if (room.Type != RoomType.Chat)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Chat room not found");
        }
        return new Envelope<ChatRoom>(_mapper.Map<ChatRoom>(room));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatRoom>> CreateChatRoomAsync(Guid gameId, CreateChatRoom input)
    {
        // 1. Create Room with Type = Chat
        var createRoom = new CreateRoom
        {
            GameId = gameId,
            Title = input.Title,
            Type = RoomType.Chat,
            AccessType = RoomAccessType.Private
        };
        var room = await _roomService.CreateAsync(createRoom);

        // 2. Create linked Chat entity with ChatType.GameRoom
        var chat = await _chatService.CreateGameRoomChatAsync(room.Id, input.Title);

        // 3. Update room with chat link
        var updateRoom = new UpdateRoom
        {
            RoomId = room.Id,
            ChatId = chat.Id
        };
        room = await _roomService.UpdateAsync(updateRoom);

        return new Envelope<ChatRoom>(_mapper.Map<ChatRoom>(room));
    }

    /// <inheritdoc />
    public async Task<Envelope<ChatRoom>> UpdateChatRoomAsync(Guid id, UpdateChatRoom input)
    {
        var room = await _roomService.GetAsync(id);
        if (room.Type != RoomType.Chat)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Chat room not found");
        }

        var updateRoom = new UpdateRoom
        {
            RoomId = id,
            Title = input.Title,
            PreviousRoomId = input.PreviousRoomId
        };
        room = await _roomService.UpdateAsync(updateRoom);

        return new Envelope<ChatRoom>(_mapper.Map<ChatRoom>(room));
    }

    /// <inheritdoc />
    public async Task DeleteChatRoomAsync(Guid id)
    {
        var room = await _roomService.GetAsync(id);
        if (room.Type != RoomType.Chat)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Chat room not found");
        }

        // Delete linked chat if exists
        if (room.ChatId.HasValue)
        {
            await _chatService.DeleteAsync(room.ChatId.Value);
        }

        await _roomService.DeleteAsync(id);
    }

    /// <inheritdoc />
    public async Task<CursorEnvelope<Message>> GetMessagesAsync(Guid chatRoomId, string? cursor, int limit)
    {
        var room = await GetChatRoomForUpdateAsync(chatRoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.ViewMessages, room);

        return await _messagingService.GetMessagesWithCursorAsync(
            room.ChatId!.Value, cursor, null, null, limit);
    }

    /// <inheritdoc />
    public async Task<Envelope<Message>> CreateMessageAsync(Guid chatRoomId, CreateMessageInput input)
    {
        var room = await GetChatRoomForUpdateAsync(chatRoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.SendMessage, room);

        var message = new Message { Text = new CommonBbText { Value = input.Text } };
        return await _messagingService.CreateMessageAsync(room.ChatId!.Value, message);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid chatRoomId)
    {
        var room = await GetChatRoomForUpdateAsync(chatRoomId);
        _intentionManager.ThrowIfForbidden(RoomIntention.ViewMessages, room);

        await _messagingService.MarkAsReadAsync(room.ChatId!.Value);
    }

    private async Task<RoomToUpdate> GetChatRoomForUpdateAsync(Guid chatRoomId)
    {
        var userId = _identityProvider.Current.User.UserId;
        var room = await _roomRepository.GetForUpdate(chatRoomId, userId);

        if (room == null || room.Type != RoomType.Chat || !room.ChatId.HasValue)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Chat room not found");
        }

        return room;
    }
}
