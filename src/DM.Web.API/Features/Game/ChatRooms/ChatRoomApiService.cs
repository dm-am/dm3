using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Messaging.Features.Chats;
using DM.Web.API.Features.Messaging;
using DM.Web.API.Features.Messaging.Messages;
using DM.Web.API.Shared.Dto;
using CreateRoom = DM.Domain.Game.Features.Rooms.CreateRoom;
using UpdateRoom = DM.Domain.Game.Features.Rooms.UpdateRoom;

namespace DM.Web.API.Features.Game.ChatRooms;

public class ChatRoomApiService
{
    private readonly IRoomService _roomService;
    private readonly IChatService _chatService;
    private readonly MessagingApiService _messagingService;
    private readonly ChatRoomMapper _mapper;

    public ChatRoomApiService(
        IRoomService roomService,
        IChatService chatService,
        MessagingApiService messagingService,
        ChatRoomMapper mapper)
    {
        _roomService = roomService;
        _chatService = chatService;
        _messagingService = messagingService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<ChatRoom>> GetChatRoomsAsync(Guid gameId)
    {
        var rooms = await _roomService.GetAllAsync(gameId);
        // Chats to enter: the rooms listing also names closed private rooms, for the lock row.
        var chatRooms = rooms
            .Where(r => r.Type == RoomType.Chat && r.CanView)
            .Select(_mapper.ToChatRoom);
        return new ListEnvelope<ChatRoom>(chatRooms);
    }

    public async Task<Envelope<ChatRoom>> GetChatRoomAsync(Guid id) =>
        new(_mapper.ToChatRoom(await GetChatRoomOrThrow(id)));

    /// <summary>A room that is not a chat answers as an unknown chat.</summary>
    private async Task<Room> GetChatRoomOrThrow(Guid id)
    {
        var room = await _roomService.GetAsync(id);
        return room.Type == RoomType.Chat
            ? room
            : throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.ChatNotFound);
    }

    public async Task<Envelope<ChatRoom>> CreateChatRoomAsync(Guid gameId, CreateChatRoom input)
    {
        var createRoom = new CreateRoom
        {
            GameId = gameId,
            Title = input.Title,
            Type = RoomType.Chat,
            AccessType = RoomAccessType.Private
        };
        var room = await _roomService.CreateAsync(createRoom);

        var chat = await _chatService.CreateGameRoomChatAsync(room.Id, input.Title);

        var updateRoom = new UpdateRoom
        {
            RoomId = room.Id,
            ChatId = chat.Id
        };
        room = await _roomService.UpdateAsync(updateRoom);

        return new Envelope<ChatRoom>(_mapper.ToChatRoom(room));
    }

    public async Task<Envelope<ChatRoom>> UpdateChatRoomAsync(Guid id, UpdateChatRoom input)
    {
        await GetChatRoomOrThrow(id);

        var updateRoom = new UpdateRoom
        {
            RoomId = id,
            Title = input.Title,
            PreviousRoomId = input.PreviousRoomId
        };
        var room = await _roomService.UpdateAsync(updateRoom);

        return new Envelope<ChatRoom>(_mapper.ToChatRoom(room));
    }

    public async Task DeleteChatRoomAsync(Guid id)
    {
        var room = await GetChatRoomOrThrow(id);
        if (room.ChatId.HasValue)
        {
            await _chatService.DeleteAsync(room.ChatId.Value);
        }

        await _roomService.DeleteAsync(id);
    }

    public async Task<CursorEnvelope<Message>> GetMessagesAsync(Guid chatRoomId, string? cursor, int limit)
    {
        var room = await _roomService.GetChatRoomForReadingAsync(chatRoomId);

        return await _messagingService.GetGameRoomMessagesWithCursorAsync(
            room.ChatId!.Value, cursor, limit);
    }

    public async Task<Envelope<Message>> CreateMessageAsync(Guid chatRoomId, MessageInput input)
    {
        var room = await _roomService.GetChatRoomForWritingAsync(chatRoomId);
        return await _messagingService.CreateGameRoomMessageAsync(room.ChatId!.Value, input.Text);
    }

    public async Task MarkAsReadAsync(Guid chatRoomId)
    {
        var room = await _roomService.GetChatRoomForReadingAsync(chatRoomId);

        await _messagingService.MarkAsReadAsync(room.ChatId!.Value);
    }
}
