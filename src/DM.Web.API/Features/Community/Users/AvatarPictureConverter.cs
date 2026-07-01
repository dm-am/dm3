using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Uploads;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// Конвертер Domain <see cref="AvatarPicture"/> (один source-key) в API
/// <see cref="UserPicture"/> (3 URL: small/medium/original).
///
/// Источник правды для размеров thumbnail'ов на API-слое: small=100,
/// medium=400. Берется ObjectKey, через <see cref="IImgproxyUrlBuilder"/>
/// собираются подписанные imgproxy URL'ы. Original остается direct-served
/// (без transform'а — он уже в правильном размере ≤1024px).
///
/// Имгпрокси на лету делает center-crop + resize + format-negotiation
/// (AVIF/WebP/JPEG по Accept header браузера) — один URL для всех форматов.
/// </summary>
internal class AvatarPictureConverter : ITypeConverter<AvatarPicture, UserPicture>
{
    /// <summary>Small thumbnail size — для inline avatars в lists/chat (24-64 px DOM).</summary>
    public const int SmallSize = 100;

    /// <summary>Medium thumbnail size — для card-view (60-200 px DOM) + retina-DPR.</summary>
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
        };
    }
}
