using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Uploads;

// A thumbnail lies beside its original under a name derived from the original's, so the key the
// store writes and the address the API hands out are the same derivation.
public class AvatarThumbnailShould
{
    [Theory]
    [InlineData("avatars/u_g.jpg", AvatarThumbnail.SmallSize, "avatars/u_g_100.webp")]
    [InlineData("http://localhost:9000/dm-uploads/characters/u_g.png", AvatarThumbnail.MediumSize,
        "http://localhost:9000/dm-uploads/characters/u_g_400.webp")]
    [InlineData("https://cdn.example.com/avatars/u_g", AvatarThumbnail.SmallSize,
        "https://cdn.example.com/avatars/u_g_100.webp")]
    public void LieBesideTheOriginalWithTheSideInPlaceOfItsExtension(string original, int size, string expected) =>
        AvatarThumbnail.AddressOf(original, size).Should().Be(expected);

    // The objects an upload occupies are the ones the sweeper deletes with it: an avatar's
    // thumbnails go with the original, and an attachment has none.
    [Theory]
    [InlineData(UploadType.UserAvatar, "avatars/u_g.jpg",
        new[] { "avatars/u_g.jpg", "avatars/u_g_100.webp", "avatars/u_g_400.webp" })]
    [InlineData(UploadType.CharacterAvatar, "characters/u_g.png",
        new[] { "characters/u_g.png", "characters/u_g_100.webp", "characters/u_g_400.webp" })]
    [InlineData(UploadType.PostAttachment, "posts/u_g.gif", new[] { "posts/u_g.gif" })]
    public void OccupyTheOriginalAndTheThumbnailsOfItsType(UploadType type, string objectKey, string[] expected) =>
        AvatarThumbnail.ObjectKeysOf(objectKey, type).Should().Equal(expected);
}
