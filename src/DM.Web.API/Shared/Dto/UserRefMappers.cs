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
    /// <remarks>
    /// Null in, null out, as <c>UserMapper.ToUser</c> does it: the domain hands
    /// users over from <c>= null!</c> members, and a removed account is nested
    /// as exactly that null (the content reads keep what it wrote and sign it
    /// by nobody). The generated body would dereference it.
    /// </remarks>
    public static UserRef ToUserRef(GeneralUser user) => user is null ? null! : ToUserRefCore(user);

    [MapProperty(nameof(GeneralUser.UserId), nameof(UserRef.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private static partial UserRef ToUserRefCore(GeneralUser user);

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
