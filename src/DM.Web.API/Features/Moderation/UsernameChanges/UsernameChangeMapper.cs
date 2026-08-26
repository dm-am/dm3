using DM.Domain.Account.Features.UsernameChange;
using Riok.Mapperly.Abstractions;
using ApiUsernameChangeRequest = DM.Web.API.Features.Moderation.UsernameChanges.UsernameChangeRequest;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// Compile-time mapper for the moderation queue of name change requests.
/// Three names differ between the two shapes and are stated here rather than
/// left to convention.
/// </summary>
[Mapper]
internal partial class UsernameChangeMapper
{
    /// <summary>
    /// Domain queue entry to the moderation queue row
    /// </summary>
    [MapProperty(nameof(UsernameChangeRequestEntry.RequestId), nameof(ApiUsernameChangeRequest.Id))]
    [MapProperty(nameof(UsernameChangeRequestEntry.ApprovalTokenExpiresUtc), nameof(ApiUsernameChangeRequest.ApprovalExpiresUtc))]
    [MapProperty(nameof(UsernameChangeRequestEntry.ResolvedByUsername), nameof(ApiUsernameChangeRequest.ResolvedBy))]
    [MapProperty(nameof(UsernameChangeRequestEntry.ResolverComment), nameof(ApiUsernameChangeRequest.Comment))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ApiUsernameChangeRequest ToRequest(UsernameChangeRequestEntry entry);
}
