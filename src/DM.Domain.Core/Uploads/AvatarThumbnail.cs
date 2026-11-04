using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// The square thumbnails of an avatar: cut by the image pipeline when the picture
/// is uploaded and stored next to the original, under its key with the side in
/// place of the extension.
/// </summary>
/// <remarks>
/// No row names them: they live and go with the upload of their original, and
/// they are served from its prefix, which reads anonymously.
/// </remarks>
public static class AvatarThumbnail
{
    /// <summary>Side of the thumbnail for inline avatars in lists and chat (24-64 px in the page).</summary>
    public const int SmallSize = 100;

    /// <summary>Side of the thumbnail for card views (60-200 px in the page) at retina density.</summary>
    public const int MediumSize = 400;

    /// <summary>Format of every thumbnail, whatever the format of the original.</summary>
    public const string ContentType = "image/webp";

    private const string Extension = ".webp";

    private static readonly int[] Sizes = [SmallSize, MediumSize];

    /// <summary>
    /// The sides cut for an upload of the type: both for an avatar, none for anything else.
    /// </summary>
    /// <param name="type">Upload type.</param>
    public static IReadOnlyList<int> SizesFor(UploadType type) =>
        type is UploadType.UserAvatar or UploadType.CharacterAvatar ? Sizes : [];

    /// <summary>
    /// Where the thumbnail of the side lies: the original's object key, or its public
    /// address, with the side in place of the extension.
    /// </summary>
    /// <param name="original">Object key or public address of the original.</param>
    /// <param name="size">Side of the thumbnail in pixels.</param>
    public static string AddressOf(string original, int size)
    {
        // The last dot of the name, not of the host the address may start with.
        var extension = original.LastIndexOf('.');
        var stem = extension > original.LastIndexOf('/') ? original[..extension] : original;
        return $"{stem}_{size}{Extension}";
    }

    /// <summary>
    /// Every object an upload of the type occupies: the original and its thumbnails.
    /// </summary>
    /// <param name="objectKey">Object key of the original.</param>
    /// <param name="type">Upload type.</param>
    public static IReadOnlyList<string> ObjectKeysOf(string objectKey, UploadType type) =>
        [objectKey, .. SizesFor(type).Select(size => AddressOf(objectKey, size))];
}
