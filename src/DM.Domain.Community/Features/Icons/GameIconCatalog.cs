using System.Collections.Generic;
using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Community.Features.Icons;

/// <summary>
/// Registry of icon names from the game-icons.net sprite. Kept in sync
/// manually with the frontend manifest <c>src/DM.Web.Client/src/shared/ui/Icon/gameIcons.ts</c>
/// when a new SVG is added. Used by Award/Achievement type validation:
/// when saving a type with an unknown name the service throws
/// 400 — an explicit rejection for the admin is better than a broken icon
/// for the user.
///
/// Attribution (CC BY 3.0): Lorc, Delapouite, Skoll, darkzaitzev and
/// game-icons.net contributors.
/// </summary>
public static class GameIconCatalog
{
    /// <summary>Allowed icon names (kebab-case, exactly as the file).</summary>
    private static readonly IReadOnlySet<string> Names = new HashSet<string>
    {
        "laurels",        // laurel wreath — summer literary contest / top tier
        "trophy-cup",     // cup — winter literary contest
        "scroll-quill",   // scroll with quill — game posts
        "hourglass",      // hourglass — years of service
        "medal",          // medal — general purpose
        "ribbon-medal",   // ribbon medal — special awards (the guessing game)
        "healing",        // healer's cross — rating (post reviews)
        "scepter",        // scepter — games as a game master
        "sword",          // sword — games as a player
        "book",           // book — blogs
        "papers",         // papers — publications (articles inside blogs)
        "stabbed-note",   // stabbed note — topics
        "discussion",     // talking heads — comments
        "talk",           // dialog — global chat
        "heart-organ",    // anatomical heart — total likes on content
        "plastic-duck",   // plastic duck — bans (the duckling-terrorists meme)
        "walking-boot",   // walking boot — drops (left the game)
        "quill-ink",      // quill in inkwell — contest "best critic" award
        "magnifying-glass", // magnifier — the guessing game (identified the most authors)
        "palette",        // palette — art contest placement (trophy-cup analog)
        "goblin",         // goblin — the "Почетный гоблин" award
    };

    /// <summary>True if the icon name is known to the sprite.</summary>
    public static bool IsValid(string iconName) =>
        !string.IsNullOrWhiteSpace(iconName) && Names.Contains(iconName);

    /// <summary>Rejects an unknown icon name with 400, for the save paths.</summary>
    public static void EnsureValid(string iconName)
    {
        if (!IsValid(iconName))
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                RefusalMessage.UnknownIconName(iconName));
        }
    }
}
