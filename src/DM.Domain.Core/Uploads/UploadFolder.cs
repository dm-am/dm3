using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The prefix each upload type is stored under, and which of those prefixes the
/// object store serves to anyone who has the address.
/// </summary>
/// <remarks>
/// Readable prefixes are named, so a type added to the enum stays private until
/// someone says otherwise.
/// </remarks>
public static class UploadFolder
{
    public static string For(UploadType type) => type switch
    {
        UploadType.UserAvatar => "avatars",
        UploadType.CharacterAvatar => "characters",
        UploadType.PostAttachment => "posts",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown upload type"),
    };

    /// <summary>
    /// Types whose objects anyone may read. An avatar is shown wherever its owner
    /// is, so restricting it would buy nothing; everything else stays closed until
    /// the product states who its audience is.
    /// </summary>
    public static IReadOnlyCollection<UploadType> AnonymouslyReadable { get; } =
        new[] { UploadType.UserAvatar, UploadType.CharacterAvatar };
}
