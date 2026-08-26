using System;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// An authorizer that answers for one upload type and refuses nothing.
/// </summary>
/// <remarks>
/// The rules it stands in for are about which authorizer is picked and what the
/// service does with the answer, so the answer itself is a constant here.
/// </remarks>
internal sealed class AllowingUploadAuthorizer : IUploadTargetAuthorizer
{
    public AllowingUploadAuthorizer(UploadType type) => Type = type;

    public UploadType Type { get; }

    public Task EnsureAllowedAsync(Guid targetId) => Task.CompletedTask;

    public Task EnsureReadAllowedAsync(Guid targetId) => Task.CompletedTask;

    public Task<bool> MayDetachAsync(Guid targetId) => Task.FromResult(true);
}
