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
internal partial class TagMapper
{
    /// <summary>
    /// Domain tag group to its response DTO
    /// </summary>
    public partial TagGroup ToTagGroup(DtoTagGroup group);

    /// <summary>
    /// Domain tag to its response DTO
    /// </summary>
    public partial Tag ToTag(DtoTag tag);

    /// <summary>
    /// Create-group request to the domain command
    /// </summary>
    public partial DtoCreateTagGroup ToCreateTagGroup(CreateTagGroupRequest request);

    /// <summary>
    /// Update-group request to the domain command. Id comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DtoUpdateTagGroup.Id))]
    public partial DtoUpdateTagGroup ToUpdateTagGroup(UpdateTagGroupRequest request);

    /// <summary>
    /// Create-tag request to the domain command
    /// </summary>
    public partial DtoCreateTag ToCreateTag(CreateTagRequest request);

    /// <summary>
    /// Update-tag request to the domain command. Id comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DtoUpdateTag.Id))]
    public partial DtoUpdateTag ToUpdateTag(UpdateTagRequest request);
}
