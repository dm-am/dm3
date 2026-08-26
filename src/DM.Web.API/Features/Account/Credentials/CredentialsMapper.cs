using DM.Domain.Account.Features.UsernameChange;
using Riok.Mapperly.Abstractions;
using ServiceCreateUsernameChangeRequest = DM.Domain.Account.Features.UsernameChange.CreateUsernameChangeRequest;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// Compile-time mapper for account credential flows
/// </summary>
[Mapper]
internal partial class CredentialsMapper
{
    /// <summary>
    /// Domain queue entry to the account-facing status DTO
    /// </summary>
    [MapProperty(nameof(UsernameChangeRequestEntry.RequestId), nameof(UsernameChangeResponse.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial UsernameChangeResponse ToResponse(UsernameChangeRequestEntry entry);

    /// <summary>
    /// Create request to the domain command. The AutoMapper predecessor had
    /// no map for this pair at all, so filing a request answered 500 the
    /// moment it reached the reply - a generated method cannot be missing.
    /// </summary>
    public partial ServiceCreateUsernameChangeRequest ToCreateRequest(UsernameChangeCreateRequest request);
}
