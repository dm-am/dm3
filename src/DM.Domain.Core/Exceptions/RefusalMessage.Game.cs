namespace DM.Domain.Core.Exceptions;

/// <summary>
/// Refusals of games, their rooms, characters, posts and reviews
/// </summary>
public static partial class RefusalMessage
{
    // Games

    /// <summary>
    /// The game is absent, or hidden from this caller
    /// </summary>
    public const string GameNotFound = "Игра не найдена";

    /// <summary>
    /// The room is absent, or belongs to a game this caller cannot read
    /// </summary>
    public const string RoomNotFound = "Комната не найдена";

    /// <summary>
    /// The per-user access grant on a room is absent
    /// </summary>
    public const string RoomAccessNotFound = "Доступ к комнате не найден";

    /// <summary>
    /// The character is absent, or belongs to a game this caller cannot read
    /// </summary>
    public const string CharacterNotFound = "Персонаж не найден";

    /// <summary>
    /// The game post is absent
    /// </summary>
    public const string PostNotFound = "Пост не найден";

    /// <summary>
    /// Who sees a file is chosen on a file of a post alone: an avatar is everybody's
    /// </summary>
    public const string AudienceOnlyForPostAttachment = "Выбрать, кто видит, можно только у файла поста";

    /// <summary>
    /// A change of who sees a file that names no choice: read as "Все", it would open the
    /// file to everybody.
    /// </summary>
    public const string AudienceNotChosen = "Выберите, кто видит файл";

    /// <summary>
    /// A character chosen to see a file, or named by its id, is not among those let into the room
    /// </summary>
    public const string CharacterNotInRoom = "Персонажа нет в этой комнате. Обновите страницу и выберите снова";

    /// <summary>
    /// A file every reader sees, put by the text only inside hidden blocks: the mark
    /// and the text would say two things, so the author chooses one
    /// </summary>
    public static string FileForAllOnlyInsideHiddenBlock(string fileName) =>
        $"Файл \"{fileName}\" виден всем, но вставлен только внутрь скрытого блока. Выберите, кто видит файл";

    /// <summary>
    /// The game's owner put this caller on its blacklist
    /// </summary>
    /// <remarks>
    /// Said by GameRoleExtensions.ThrowIfBlacklisted and by nothing else: both
    /// actions the list closes, the discussion and the application, ask that one
    /// method, so the answer reads the same under either.
    /// </remarks>
    public const string BlacklistedFromGame = "Вы в черном списке этой игры";

    /// <summary>
    /// One review per author per game
    /// </summary>
    public const string AlreadyReviewedGame = "Вы уже оставили рецензию на эту игру";

    /// <summary>
    /// One review per author per post
    /// </summary>
    public const string AlreadyReviewedPost = "Вы уже оценили этот пост";

    /// <summary>
    /// Probation: below the post threshold a rating may only be neutral. Said
    /// both when the review is written and when an edit tries to move its sign,
    /// which is the same rule reached by two doors
    /// </summary>
    public static string SignedReviewNeedsExperience(int postThreshold) =>
        $"Ставить плюс и минус можно после {postThreshold} постов в играх";
}
