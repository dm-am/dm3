using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DtoCreateTag = DM.Domain.Moderation.Features.Tags.CreateTag;
using DtoCreateTagGroup = DM.Domain.Moderation.Features.Tags.CreateTagGroup;
using DtoTag = DM.Domain.Moderation.Features.Tags.Tag;
using DtoTagGroup = DM.Domain.Moderation.Features.Tags.TagGroup;
using DtoUpdateTag = DM.Domain.Moderation.Features.Tags.UpdateTag;
using DtoUpdateTagGroup = DM.Domain.Moderation.Features.Tags.UpdateTagGroup;

namespace DM.Web.API.Features.Moderation.Tags;

/// <summary>
/// Compile-time mapper for tag administration
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class TagMapper
{
    private readonly IIdentityProvider _identityProvider;

    public TagMapper(IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    /// <summary>
    /// Domain tag group to its response DTO
    /// </summary>
    public partial TagGroup ToTagGroup(DtoTagGroup group);

    /// <summary>
    /// Domain tag to its response DTO, with the reader bound to the description
    /// as its author: every reader of the catalogue is a moderator who may edit
    /// it, so the edit window gets the source with the author_edit audience
    /// </summary>
    public Tag ToTag(DtoTag tag)
    {
        var result = ToTagCore(tag);
        result.Description?.BindAuthor(AnonymousIdentity.Of(_identityProvider.Current?.User));
        return result;
    }

    private partial Tag ToTagCore(DtoTag tag);

    /// <summary>
    /// Create-group request to the domain command
    /// </summary>
    public partial DtoCreateTagGroup ToCreateTagGroup(TagGroupRequest request);

    /// <summary>
    /// Update-group request to the domain command. Id comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DtoUpdateTagGroup.Id))]
    public partial DtoUpdateTagGroup ToUpdateTagGroup(TagGroupRequest request);

    /// <summary>
    /// Create-tag request to the domain command
    /// </summary>
    public partial DtoCreateTag ToCreateTag(TagRequest request);

    /// <summary>
    /// Update-tag request to the domain command. Id comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DtoUpdateTag.Id))]
    public partial DtoUpdateTag ToUpdateTag(TagRequest request);
}
