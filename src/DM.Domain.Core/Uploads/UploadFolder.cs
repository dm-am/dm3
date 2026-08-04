using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The prefix each upload type is stored under, and which of those prefixes the
/// object store serves to anyone who has the address.
/// </summary>
/// <remarks>
/// The bucket policy used to grant anonymous reads on the whole bucket while its
/// comment spoke only about avatars — true of the one type that had a producer,
/// and a decision nobody made about the other two. Naming the readable prefixes
/// here makes a type added to the enum private until someone says otherwise,
/// instead of public because nobody said anything.
/// </remarks>
public static class UploadFolder
{
    /// <summary>
    /// Prefix the type's objects live under.
    /// </summary>
    /// <param name="type">Upload type.</param>
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
