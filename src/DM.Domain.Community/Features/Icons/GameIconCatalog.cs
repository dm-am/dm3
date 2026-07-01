using System.Collections.Generic;

namespace DM.Domain.Community.Features.Icons;

/// <summary>
/// Реестр имен иконок из game-icons.net спрайта. Синхронизируется
/// вручную с frontend-манифестом <c>src/DM.Web.Client/src/shared/ui/Icon/gameIcons.ts</c>
/// при добавлении новой SVG. Используется валидацией Award/Achievement
/// типов: при попытке сохранить тип с неизвестным именем сервис кидает
/// 400 — лучше явный отказ для админа, чем сломанная иконка для
/// пользователя.
///
/// Атрибуция (CC BY 3.0): Lorc, Delapouite, Skoll, darkzaitzev и
/// контрибьюторы game-icons.net.
/// </summary>
public static class GameIconCatalog
{
    /// <summary>Допустимые имена иконок (kebab-case, ровно как файл).</summary>
    public static readonly IReadOnlySet<string> Names = new HashSet<string>
    {
        "laurels",        // лавровый венок — летний литконкурс / высший тир
        "trophy-cup",     // кубок — зимний литконкурс
        "scroll-quill",   // свиток с пером — игровые посты
        "hourglass",      // песочные часы — выслуга лет
        "medal",          // медаль — general purpose
        "ribbon-medal",   // медаль на ленте — спец-награды (угадайка)
        "healing",        // целительский крест — рейтинг (постовые отзывы)
        "scepter",        // скипетр — игры в роли ведущего
        "sword",          // меч — игры в роли игрока
        "book",           // книга — блоги
        "papers",         // бумаги — публикации (статьи внутри блогов)
        "stabbed-note",   // заколотая записка — топики
        "discussion",     // беседующие головы — комментарии
        "talk",           // диалог — глобальный чат
        "heart-organ",    // анатомическое сердце — суммарные лайки на контенте
        "plastic-duck",   // пластиковая уточка — баны (мем про утят-террористов)
        "walking-boot",   // походный сапог — дропы (ушел из игры)
        "quill-ink",      // перо в чернильнице — «лучший критик» конкурса
        "magnifying-glass", // лупа — «угадайка» (определил больше всех авторов)
        "palette",        // палитра — место в арт-конкурсе (аналог trophy-cup)
    };

    /// <summary>True если имя иконки известно спрайту.</summary>
    public static bool IsValid(string iconName) =>
        !string.IsNullOrWhiteSpace(iconName) && Names.Contains(iconName);
}
