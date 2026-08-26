using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Shared user-reference conversions for Mapperly mappers, consumed directly
/// or via <c>[UseStaticMapper(typeof(UserRefMappers))]</c>. Ref projections
/// narrow by design, so only target members are required: the compile-time
/// check guards every <see cref="UserRef"/> field having a source, not the
/// sources being exhausted.
/// </summary>
[Mapper]
internal static partial class UserRefMappers
{
    /// <summary>General user to the lightweight reference</summary>
    [MapProperty(nameof(GeneralUser.UserId), nameof(UserRef.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial UserRef ToUserRef(GeneralUser user);

    /// <summary>Domain user reference to the API one</summary>
    [MapProperty(nameof(UserReference.UserId), nameof(UserRef.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial UserRef ToUserRef(UserReference user);

    /// <summary>Game assistant to the lightweight reference</summary>
    [MapProperty(nameof(GameAssistantInfo.UserId), nameof(UserRef.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial UserRef ToUserRef(GameAssistantInfo assistant);

    /// <summary>Blog assistant to the lightweight reference</summary>
    [MapProperty(nameof(BlogAssistantInfo.UserId), nameof(UserRef.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial UserRef ToUserRef(BlogAssistantInfo assistant);
}
