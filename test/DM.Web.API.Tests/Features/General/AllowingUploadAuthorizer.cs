using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;

namespace DM.Web.API.Tests.Features.General;

// The rules it stands in for are about which authorizer is picked, so the answer is a constant.
internal sealed class AllowingUploadAuthorizer(UploadType type, bool mayDetach = true) : IUploadTargetAuthorizer
{
    public UploadType Type { get; } = type;

    public bool DetachAsked { get; private set; }

    public Task EnsureAllowedAsync(Guid targetId) => Task.CompletedTask;

    public Task EnsureReadAllowedAsync(StoredUpload upload) => Task.CompletedTask;

    public Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    public Task<bool> OpenToGuestAsync(StoredUpload upload) => Task.FromResult(true);

    public Task<string?> FreezeAudienceAsync(Guid targetId, Guid? uploadId, UploadAudience audience) =>
        Task.FromResult<string?>(null);

    public Task<bool> MayDetachAsync(Guid targetId)
    {
        DetachAsked = true;
        return Task.FromResult(mayDetach);
    }
}
