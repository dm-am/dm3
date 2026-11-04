using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// UI test fixtures for the designated game: post pendency, unread counter, closed room posts,
    /// chat room messages and master notepad entries, so a reseed gives QA the same game every time.
    /// </summary>
    private async Task SeedGameZoneFixtures(
        SeedingGame seeding, GameRooms rooms, GameCast cast, List<DbUser> users,
        List<(Chat Chat, Guid LastMessageId)> chatsAwaitingLastMessage,
        ComprehensiveSeedResult result)
    {
        var (game, master, _, variation, _) = seeding;
        var playersForGame = cast.Players;
        var restrictedRoomMembers = cast.RoomMembers;

        var waitingPlayer = playersForGame[0];
        var waitingCharacter = cast.Characters[0];

        SeedTurnPendency(rooms.Main, waitingCharacter, waitingPlayer, master);
        SeedRoomUnreadCounter(game, rooms.Main, variation.Posts);
        SeedClosedRoomScene(rooms.Private, cast.Npc, restrictedRoomMembers, master, users, result);

        // Open chat room: a short OOC exchange, so the chat page and its pagination have something to render.
        await SeedRoomChat(
            rooms.Chat,
            new[] { master.UserId }.Concat(playersForGame.Take(2).Select(p => p.UserId)).ToList(),
            new[]
            {
                "Народ, всем удобно новое время постинга?",
                "Да, вроде норм, буду успевать чаще писать.",
                "Класс! Тогда продолжаем в том же духе.",
                "Кстати, кто-нибудь помнит, где мы в прошлый раз остановились?",
                "Я вроде помню - у ворот перед встречей с торговцем.",
                "Точно, спасибо! Сейчас напишу пост.",
            },
            chatsAwaitingLastMessage, result);

        // Closed chat room: the same kind of exchange, only between the master and the players with access.
        await SeedRoomChat(
            rooms.PrivateChat!,
            new[] { master.UserId }
                .Concat(restrictedRoomMembers.Take(2).Select(c => c.AuthorId ?? master.UserId))
                .ToList(),
            new[]
            {
                "Тут только те, у кого есть доступ. Про алтарь при остальных не пишем.",
                "Принято. Мой персонаж делает вид, что ничего не заметил.",
                "А жрец успеет добежать до алтаря за один ход?",
                "Успеет, если не потратите ход на спор у двери.",
            },
            chatsAwaitingLastMessage, result);

        SeedMasterNotepad(game, master);
        SeedCharacterNotepads(game, master, waitingCharacter, waitingPlayer);
    }

    /// <summary>
    /// Open post pendency awaiting a seeded player - shows the red
    /// waiting-star with tooltip until they post or it's fulfilled.
    /// </summary>
    private void SeedTurnPendency(
        Room mainRoom, Character waitingCharacter, DbUser waitingPlayer, DbUser master)
    {
        _dbContext.Set<PostPendency>().Add(new PostPendency
        {
            PendencyId = _guidFactory.Create(),
            RoomId = mainRoom.RoomId,
            CharacterId = waitingCharacter.CharacterId,
            WaitingForUserId = waitingPlayer.UserId,
            CreatedById = master.UserId,
            CreatedUtc = _now.AddDays(-2),
            FulfilledUtc = null,
            LastReminderUtc = null
        });
    }

    /// <summary>
    /// Baseline unread counter for the main room so any user who
    /// never visited it (i.e. everyone but its own post authors)
    /// sees the (N) unread badge instead of a silent zero.
    /// </summary>
    private void SeedRoomUnreadCounter(DbGame game, Room mainRoom, int postsToCreate)
    {
        if (postsToCreate > 0)
        {
            _dbContext.UnreadCounters.Add(new UnreadCounter
            {
                UserId = Guid.Empty,
                EntityId = mainRoom.RoomId,
                ParentId = game.GameId,
                EntryType = UnreadEntryType.Message,
                LastReadUtc = game.ActivatedUtc!.Value.UtcDateTime,
                Counter = Math.Min(postsToCreate, 5),
            });
        }
    }

    /// <summary>
    /// The scene of the closed post room, written only by the NPC and the characters with access.
    /// </summary>
    private void SeedClosedRoomScene(
        Room privateRoom, Character npc, List<Character> restrictedRoomMembers, DbUser master, List<DbUser> users,
        ComprehensiveSeedResult result)
    {
        var closedRoomPostTexts = new[]
        {
            "Тяжелая дверь закрывается за вашими спинами. Камень на алтаре светится ровным холодным светом.",
            "Подхожу ближе и осматриваю алтарь, стараясь ничего не задеть.",
            "Встаю у двери и слушаю коридор. Если кто-то пойдет следом, услышу первым.",
        };
        var closedRoomAuthors = new[] { npc }.Concat(restrictedRoomMembers).ToList();
        for (var pi = 0; pi < closedRoomPostTexts.Length; pi++)
        {
            var closedRoomCharacter = closedRoomAuthors[pi % closedRoomAuthors.Count];
            var closedRoomAuthorId = closedRoomCharacter.AuthorId ?? master.UserId;
            _dbContext.Set<Post>().Add(new Post
            {
                PostId = _guidFactory.Create(),
                RoomId = privateRoom.RoomId,
                CharacterId = closedRoomCharacter.CharacterId,
                AuthorId = closedRoomAuthorId,
                CreatedUtc = _now.AddDays(-(closedRoomPostTexts.Length - pi)),
                GameText = closedRoomPostTexts[pi],
            });
            result.PostsCreated++;
            users.First(u => u.UserId == closedRoomAuthorId).QuantityRating++;
        }
    }

    /// <summary>
    /// A chat room filled with messages; both chat room examples are filled through this one.
    /// </summary>
    private async Task SeedRoomChat(
        Room room, IReadOnlyList<Guid> speakerIds, IReadOnlyList<string> texts,
        List<(Chat Chat, Guid LastMessageId)> chatsAwaitingLastMessage,
        ComprehensiveSeedResult result)
    {
        // The serial comes from the sequence before the insert, as in the repository.
        var roomChatSerialNumber = await SerialNumberAllocator.NextAsync<Chat>(_dbContext);
        var roomChat = new Chat
        {
            ChatId = _guidFactory.Create(),
            Type = ChatType.GameRoom,
            Title = room.Title,
            RoomId = room.RoomId,
            SerialNumber = roomChatSerialNumber,
            PublicId = _publicIdService.Encode(roomChatSerialNumber)
        };
        _dbContext.Set<Chat>().Add(roomChat);
        room.ChatId = roomChat.ChatId;

        Message? lastRoomMessage = null;
        for (var mi = 0; mi < texts.Count; mi++)
        {
            var roomMessage = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = speakerIds[mi % speakerIds.Count],
                ChatId = roomChat.ChatId,
                CreatedUtc = _now.AddHours(-(texts.Count - mi) * 3),
                Text = texts[mi],
            };
            _dbContext.Set<Message>().Add(roomMessage);
            lastRoomMessage = roomMessage;
            result.MessagesCreated++;
        }

        if (lastRoomMessage != null)
        {
            chatsAwaitingLastMessage.Add((roomChat, lastRoomMessage.MessageId));
        }
    }

    /// <summary>Master notepad entries (game "Заметки").</summary>
    private void SeedMasterNotepad(DbGame game, DbUser master)
    {
        var masterNotepadEntries = new[]
        {
            ("Зацепки сюжета", "Торговец на въезде в город знает больше, чем говорит - потянуть за эту нить через пару постов."),
            ("NPC на подхвате", "Таинственный Незнакомец - держать интригу, не раскрывать личность раньше времени."),
            ("Заметка для себя", "Не забыть напомнить группе про открытую заявку на персонажа - висит уже несколько дней."),
        };
        AddNotepadEntries(NotepadType.Master, game.GameId, null, master.UserId, 0, masterNotepadEntries);
    }

    /// <summary>
    /// The notes about the first active character: the player's own, so the
    /// player scope of the game "Заметки" page has data too, and the ones the
    /// leads keep about the same character.
    /// </summary>
    private void SeedCharacterNotepads(
        DbGame game, DbUser master, Character waitingCharacter, DbUser waitingPlayer)
    {
        var playerNotepadEntries = new[]
        {
            ("План на арку", "Разговорить торговца у ворот и выяснить, что он скрывает - мой персонаж ему не доверяет."),
            ("Список долгов", "Должен трактирщику 12 золотых. Вернуть после следующей вылазки, пока он сам не вспомнил."),
        };
        AddNotepadEntries(NotepadType.Player, game.GameId, waitingCharacter.CharacterId,
            waitingCharacter.AuthorId ?? waitingPlayer.UserId, 2, playerNotepadEntries);

        // The leads' notes about the same character, which has a player: the notepad is not an NPC feature.
        var characterMasterNotepadEntries = new[]
        {
            ("Что игрок не знает", "Персонаж связан с торговцем у ворот родством - раскрыть, когда группа доберется до города."),
            ("Игровые заметки", "Отыгрывает осторожно, любит разведку. Давать больше зацепок через наблюдение, меньше через прямую подсказку."),
        };
        AddNotepadEntries(NotepadType.CharacterMaster, game.GameId, waitingCharacter.CharacterId,
            master.UserId, 3, characterMasterNotepadEntries);
    }

    /// <summary>
    /// Notepad entries in their order, a day apart and ending yesterday, shifted by the given hours.
    /// </summary>
    private void AddNotepadEntries(
        NotepadType type, Guid gameId, Guid? ownerId, Guid authorId, int hoursLater, (string Title, string Content)[] entries)
    {
        for (var ni = 0; ni < entries.Length; ni++)
        {
            var (title, content) = entries[ni];
            _dbContext.Set<NotepadEntry>().Add(new NotepadEntry
            {
                EntryId = _guidFactory.Create(),
                NotepadType = type,
                ContainerId = gameId,
                OwnerId = ownerId,
                AuthorId = authorId,
                Title = title,
                Content = content,
                SortOrder = ni,
                CreatedUtc = _now.AddDays(-(entries.Length - ni)).AddHours(hoursLater),
            });
        }
    }
}
