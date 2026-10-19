using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The rooms of a game: the three every game has, the closed chat room of the
    /// UI test data game, and the two lists access is granted over.
    /// </summary>
    private sealed record GameRooms(
        Room Main, Room Chat, Room Private, Room? PrivateChat, List<Room> Restricted, List<Room> Open);

    /// <summary>
    /// Room names from the game setting, so the seed reads as a tabletop session;
    /// the "За ширмой" pool for a setting without its own.
    /// </summary>
    private static (string Main, string Chat, string Secret) RoomTitles(string setting) => setting switch
    {
        "Forgotten Realms" => ("Таверна 'Полумесяц'", "Кулуары авантюристов", "Тайный алтарь"),
        "Golarion" => ("Постоялый двор", "Беседка мастера", "Тайная библиотека"),
        "Ravenloft" => ("Замок Равенлофт", "Кабинет у камина", "Крипта"),
        "Deadlands" => ("Салун 'Кровавая Мэри'", "Крыльцо салуна", "Заброшенная шахта"),
        "Rokugan" => ("Чайная комната", "Сад камней", "Тайная келья"),
        "1920s Arkham" => ("Гостиная профессора", "Читальный зал", "Запретный архив"),
        "Dark Millennium" => ("Рубка 'Справедливости'", "Казарма экипажа", "Криптекс"),
        "Night City 2077" => ("Бар 'Афтерлайф'", "Переулок NC", "Темная клиника"),
        "Mythic Scandinavia" => ("Длинный дом", "Костер йотунов", "Руны предков"),
        "Theah" => ("Капитанская каюта", "Нижняя палуба", "Тайник капитана"),
        "The Witcher" => ("Трактир 'Серебряный медведь'", "Лагерь у костра", "Подземелье знахаря"),
        "Fallout" => ("Убежище", "Радиорубка", "Тайный склад"),
        "Post-Apocalypse Moscow" => ("Станция 'ВДНХ'", "Костер в туннеле", "Забытый бункер"),
        "Modern Nights" => ("Клуб 'Эль Дорадо'", "Задний двор", "Тайное убежище"),
        "Modern Gothic" => ("Особняк у кладбища", "Галерея портретов", "Подвал хозяина"),
        "Camelot" => ("Большой зал Камелота", "Часовня Грааля", "Тайная палата короля"),
        "Arthurian" => ("Скрипторий", "Дубрава друидов", "Обитель отшельника"),
        "Ancient Egypt" => ("Храм Ра", "Двор пирамиды", "Саркофаг"),
        "Dark Sun" => ("Оазис Балик", "Тень скалы", "Пещера джинна"),
        "Al-Qadim" => ("Базар Хуззузы", "Сад визиря", "Потайной проход"),
        "Solar System 2350" => ("Рубка 'Ареса'", "Кают-компания", "Грузовой трюм"),
        "Far Future" => ("Мостик крейсера", "Обсервационная палуба", "Трюм"),
        "Military Sci-Fi" => ("Рубка десантного бота", "Кают-компания", "Оружейная"),
        "Cyberpunk Future" => ("Бар 'Неон'", "Задворки сети", "Серверная комната"),
        "Ravnica" => ("Зал гильдии", "Уличный рынок", "Подвалы Ордрувьяра"),
        _ => ("Главная сцена", "За ширмой", "Тайная комната"),
    };

    /// <summary>The rooms of a game, in the order their OrderNumber gives them.</summary>
    private GameRooms SeedGameRooms(SeedingGame seeding)
    {
        var game = seeding.Game;
        var (mainRoomTitle, chatRoomTitle, secretRoomTitle) = RoomTitles(seeding.Template.Setting);

        var mainRoom = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = mainRoomTitle,
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            RoomNumber = 1,
            OrderNumber = 1,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
        };
        _dbContext.Set<Room>().Add(mainRoom);

        var chatRoom = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = chatRoomTitle,
            AccessType = RoomAccessType.Open,
            Type = RoomType.Chat,
            RoomNumber = 2,
            OrderNumber = 2,
            ViewPrivateText = true,
            ViewDiceResults = true,
            DiceEnabled = false,
        };
        _dbContext.Set<Room>().Add(chatRoom);

        var privateRoom = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = secretRoomTitle,
            AccessType = RoomAccessType.Private,
            Type = RoomType.Default,
            RoomNumber = 3,
            OrderNumber = 3,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
        };
        _dbContext.Set<Room>().Add(privateRoom);

        // Every closed room grants the same characters, so the closed examples differ by room type alone.
        var restrictedRooms = new List<Room> { privateRoom };

        // Openness is about reading: an open room is written in only by the characters added to it.
        var openRooms = new List<Room> { mainRoom, chatRoom };

        Room? privateChatRoom = null;
        if (seeding.IsUiTestDataGame)
        {
            privateChatRoom = SeedUiTestRooms(game, openRooms, restrictedRooms);
        }

        return new GameRooms(mainRoom, chatRoom, privateRoom, privateChatRoom, restrictedRooms, openRooms);
    }

    /// <summary>
    /// UI test fixtures for the room list: the archived room for the "archived rooms" spoiler, and the
    /// closed chat room, the fourth example of the game menu. Only this game has a chat behind its rooms.
    /// </summary>
    /// <returns>The closed chat room.</returns>
    private Room SeedUiTestRooms(DbGame game, List<Room> openRooms, List<Room> restrictedRooms)
    {
        var archivedRoom = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = "Пролог",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            RoomNumber = 4,
            OrderNumber = 4,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
            IsArchived = true,
        };
        _dbContext.Set<Room>().Add(archivedRoom);
        openRooms.Add(archivedRoom);

        // The open chat room field for field, access aside: the pair is there to be compared.
        var privateChatRoom = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = "Шепот у алтаря",
            AccessType = RoomAccessType.Private,
            Type = RoomType.Chat,
            RoomNumber = 5,
            OrderNumber = 5,
            ViewPrivateText = true,
            ViewDiceResults = true,
            DiceEnabled = false,
        };
        _dbContext.Set<Room>().Add(privateChatRoom);
        restrictedRooms.Add(privateChatRoom);

        return privateChatRoom;
    }

    /// <summary>
    /// Room access of a character, in the closed rooms and in the open ones:
    /// openness is about reading, and whoever writes in a room was added to it.
    /// </summary>
    private void GrantRoomAccess(GameRooms rooms, Character character)
    {
        foreach (var grantedRoom in rooms.Restricted.Concat(rooms.Open))
        {
            _dbContext.Set<RoomAccess>().Add(new RoomAccess
            {
                AccessId = _guidFactory.Create(),
                RoomId = grantedRoom.RoomId,
                CharacterId = character.CharacterId,
                ReaderUserId = null,
                // The enum defaults to NoAccess, and the policy is what admits writing.
                Policy = RoomAccessPolicy.Full
            });
        }
    }
}
