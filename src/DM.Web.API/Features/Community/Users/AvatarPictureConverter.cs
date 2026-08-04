using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Uploads;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// Converter of the Domain <see cref="AvatarPicture"/> (single source key) to the API
/// <see cref="UserPicture"/> (3 URL: small/medium/original).
///
/// Source of truth for thumbnail sizes at the API layer: small=100,
/// medium=400. Takes the ObjectKey and builds signed imgproxy URLs via
/// <see cref="IImgproxyUrlBuilder"/>. The original stays direct-served
/// (no transform — it is already at the right size ≤1024px).
///
/// imgproxy does on-the-fly center-crop + resize + format negotiation
/// (AVIF/WebP/JPEG by the browser Accept header) — one URL for all formats.
/// </summary>
internal class AvatarPictureConverter : ITypeConverter<AvatarPicture, UserPicture>
{
    /// <summary>Small thumbnail size — for inline avatars in lists/chat (24-64 px DOM).</summary>
    public const int SmallSize = 100;

    /// <summary>Medium thumbnail size — for card views (60-200 px DOM) + retina DPR.</summary>
    public const int MediumSize = 400;

    private readonly IImgproxyUrlBuilder _imgproxy;

    /// <inheritdoc />
    public AvatarPictureConverter(IImgproxyUrlBuilder imgproxy)
    {
        _imgproxy = imgproxy;
    }

    /// <inheritdoc />
    public UserPicture Convert(AvatarPicture source, UserPicture destination, ResolutionContext context)
    {
        if (source == null || string.IsNullOrEmpty(source.SourceObjectKey))
        {
            return new UserPicture();
        }

        return new UserPicture
        {
            SmallUrl = _imgproxy.BuildSquareThumbnail(source.SourceObjectKey, SmallSize),
            MediumUrl = _imgproxy.BuildSquareThumbnail(source.SourceObjectKey, MediumSize),
            OriginalUrl = source.SourceUrl,
            // Only for the original. The two thumbnails are square center-crops
            // at a size the caller picks, so their dimensions are not news; the
            // original keeps the uploaded aspect ratio and is the one a layout
            // cannot size without being told.
            OriginalWidth = source.SourceWidth,
            OriginalHeight = source.SourceHeight,
        };
    }
}
