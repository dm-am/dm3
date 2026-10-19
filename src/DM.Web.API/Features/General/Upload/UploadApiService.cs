using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using UploadDto = DM.Web.API.Shared.Dto.Upload;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// API service for file upload management.
/// Direct upload only: the server validates, processes (for images — generates
/// thumbnails) and puts to S3 atomically. No presigned URLs.
/// </summary>
public partial class UploadApiService
{
    private readonly IUploadRepository _uploadRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly IUserService _userService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IObjectStorage _objectStorage;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ICache _cache;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IReadOnlyCollection<IUploadTargetAuthorizer> _targetAuthorizers;

    public UploadApiService(
        IUploadRepository uploadRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager,
        IUserService userService,
        IDateTimeProvider dateTimeProvider,
        IObjectStorage objectStorage,
        IImageProcessingService imageProcessingService,
        ICache cache,
        IHttpContextAccessor httpContext,
        IEnumerable<IUploadTargetAuthorizer> targetAuthorizers)
    {
        _uploadRepository = uploadRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
        _userService = userService;
        _dateTimeProvider = dateTimeProvider;
        _objectStorage = objectStorage;
        _imageProcessingService = imageProcessingService;
        _cache = cache;
        _httpContext = httpContext;
        _targetAuthorizers = targetAuthorizers.ToList();
    }

    /// <summary>List uploads (current user; specific user — moderator+; all — admin).</summary>
    public async Task<(IEnumerable<UploadDto> Uploads, PagingInfo Paging)> GetUploads(
        UploadsQuery query, string? username, bool all)
    {
        var currentUserId = _identityProvider.Current.User.UserId;

        // Admin scope=all → all uploads in the system.
        if (all)
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListAll);
            return await GetUploadsInternal(query, userId: null);
        }

        // Moderator+ username=... → uploads of a specific user.
        if (!string.IsNullOrWhiteSpace(username))
        {
            _intentionManager.ThrowIfForbidden(UploadIntention.ListUser);
            var user = await _userService.GetAsync(username);
            return await GetUploadsInternal(query, user.UserId);
        }

        // Default → own uploads.
        return await GetUploadsInternal(query, currentUserId);
    }

    /// <summary>Get upload by ID (owner or moderator+).</summary>
    /// <remarks>
    /// Owner self-view; viewing another user's file is a moderation action. Refused
    /// with 403 only to a caller who reads the file where it hangs, and to anybody
    /// else answered as a file that does not exist, as its bytes are.
    /// </remarks>
    public async Task<UploadDto> GetUpload(Guid id)
    {
        var upload = await _uploadRepository.GetAsync(id) ?? throw UploadNotFound();
        if (!_intentionManager.IsAllowed(UploadIntention.View, upload))
        {
            await RefuseAsync(UploadIntention.View, upload);
        }

        return (await ForCaller([upload])).Single();
    }

    /// <summary>
    /// Change who sees a file of a post (its author only).
    /// </summary>
    /// <remarks>
    /// A file the caller may not have is answered as one that does not exist, as its bytes are
    /// (GetUploadContent), and so is every refusal of its post to find it: a caller who reads the
    /// file and its post and did not write the post gets 403. The module then freezes the
    /// characters chosen against the room, and the mark is written alone, the file and its place
    /// in the text untouched.
    /// </remarks>
    public async Task<UploadDto> UpdateAudience(Guid id, UploadAudience audience)
    {
        var upload = await _uploadRepository.GetAsync(id) ?? throw UploadNotFound();
        await EnsureReadableAsync(upload);
        if (upload.Type != UploadType.PostAttachment || upload.TargetId == null)
        {
            throw AudienceOnlyForPostAttachment();
        }

        var authorizer = AuthorizerFor(upload.Type);
        string? addressees;
        try
        {
            await authorizer.EnsureAllowedAsync(upload.TargetId.Value);
            addressees = await authorizer.FreezeAudienceAsync(upload.TargetId.Value, upload.Id, audience);
        }
        catch (HttpException missing) when (missing.StatusCode == HttpStatusCode.NotFound)
        {
            throw UploadNotFound();
        }

        var stored = await _uploadRepository.SetAddresseesAsync(id, addressees) ?? throw UploadNotFound();
        return MapToDto(stored, canOpen: true);
    }

    /// <summary>
    /// The file's bytes, for a caller entitled to them.
    /// </summary>
    /// <remarks>
    /// Answers <see cref="DM.Domain.Core.Exceptions.HttpException"/> with 404
    /// whenever the caller may not have the file, rather than 403: the closed
    /// prefixes exist so that an outsider learns nothing about what a private game
    /// contains, and "you may not have this" is something.
    /// </remarks>
    /// <param name="id">Upload identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<UploadContent> GetUploadContent(Guid id, CancellationToken ct = default)
    {
        var upload = await _uploadRepository.GetAsync(id) ?? throw UploadNotFound();
        await EnsureReadableAsync(upload);

        // A guest just passed the guest's read; anybody else asks it as well.
        var openToGuest = !_identityProvider.Current.User.IsAuthenticated ||
                          (upload.TargetId != null && await AuthorizerFor(upload.Type).OpenToGuestAsync(upload));

        // Read after the decision, never before: a store lookup for a caller with
        // no right to the answer is work an outsider gets to spend.
        var objectKey = await _uploadRepository.GetObjectKeyAsync(id);
        var stored = objectKey == null
            ? null
            : await _objectStorage.OpenReadAsync(objectKey, ct);

        if (stored == null)
        {
            // A row whose object is gone: to the caller, a file that is not there.
            throw UploadNotFound();
        }

        return new UploadContent(
            stored.Content,
            // The row's content type, not the store's: the row records what the
            // magic-byte validation decided, which is the only claim about this
            // file anybody checked.
            string.IsNullOrEmpty(upload.ContentType) ? stored.ContentType : upload.ContentType,
            stored.Length,
            upload.FileName,
            openToGuest);
    }

    /// <summary>Soft-delete upload (owner, moderator+, or an editor of the entity it hangs on).</summary>
    public async Task DeleteUpload(Guid id)
    {
        var userId = _identityProvider.Current.User.UserId;
        var upload = await _uploadRepository.GetAsync(id) ?? throw UploadNotFound();

        // The owner, Moderator+, and whoever may edit the entity the file hangs on:
        // an attachment is part of what a post says.
        if (!_intentionManager.IsAllowed(UploadIntention.Delete, upload) &&
            !await MayDetachAsync(upload))
        {
            await RefuseAsync(UploadIntention.Delete, upload);
        }

        await _uploadRepository.SoftDeleteAsync(id, userId, _dateTimeProvider.Now);
    }

    /// <summary>
    /// Refuses an action on the file the caller has no right to: with 403 where he
    /// sees the file, as one that does not exist where he does not.
    /// </summary>
    /// <remarks>
    /// Seeing is the file's own right to view it or the entity it hangs on letting
    /// him read it.
    /// </remarks>
    private async Task RefuseAsync(UploadIntention intention, StoredUpload upload)
    {
        if (!_intentionManager.IsAllowed(UploadIntention.View, upload))
        {
            await EnsureReadableAsync(upload);
        }

        _intentionManager.ThrowIfForbidden(intention, upload);
    }

    /// <summary>
    /// Refuses the caller who may not have the file as a file that does not exist.
    /// </summary>
    /// <remarks>
    /// The file's own rule first, the cheap one: the owner, and Moderator+, who
    /// moderates no game blind, pass the entity, though not what it keeps from all
    /// its readers but some. Otherwise the entity the file hangs on decides. A
    /// refusal names the target and that a live row exists, so it is replaced by this one.
    /// </remarks>
    private async Task EnsureReadableAsync(StoredUpload upload)
    {
        try
        {
            if (_intentionManager.IsAllowed(UploadIntention.View, upload))
            {
                if ((await WithheldAsync([upload])).Count > 0)
                {
                    throw UploadNotFound();
                }
            }
            else
            {
                await AuthorizeReadAsync(upload);
            }
        }
        catch (HttpException)
        {
            throw UploadNotFound();
        }
    }

    /// <summary>
    /// Refuses a read of a file whose target the caller cannot see, or that the
    /// target keeps from him.
    /// </summary>
    private async Task AuthorizeReadAsync(StoredUpload upload)
    {
        if (upload.TargetId == null)
        {
            // Unreachable under the CHECK constraint; a row that got there another way stays closed.
            throw UploadNotFound();
        }

        await AuthorizerFor(upload.Type).EnsureReadAllowedAsync(upload);
    }

    /// <summary>
    /// The uploads of <paramref name="uploads"/> their targets keep from a caller whose own right
    /// reaches them: one question per type, for a whole page.
    /// </summary>
    /// <remarks>A row that hangs on nothing has nothing to keep it, and the right stands.</remarks>
    private async Task<IReadOnlySet<Guid>> WithheldAsync(IReadOnlyCollection<StoredUpload> uploads)
    {
        var withheld = new HashSet<Guid>();
        foreach (var ofType in uploads.Where(u => u.TargetId != null).GroupBy(u => u.Type))
        {
            withheld.UnionWith(await AuthorizerFor(ofType.Key).WithheldAsync(ofType.ToArray()));
        }

        return withheld;
    }

    /// <summary>
    /// The uploads as the caller, whose own right reaches each of them, gets them: a file its target
    /// keeps from him is named and not opened.
    /// </summary>
    private async Task<IReadOnlyList<UploadDto>> ForCaller(IReadOnlyCollection<StoredUpload> uploads)
    {
        var withheld = await WithheldAsync(uploads);
        return uploads.Select(u => MapToDto(u, canOpen: !withheld.Contains(u.Id))).ToArray();
    }

    /// <summary>
    /// Whether the entity the file hangs on grants the caller the right to take
    /// it off.
    /// </summary>
    private async Task<bool> MayDetachAsync(StoredUpload upload) =>
        upload.TargetId != null &&
        await AuthorizerFor(upload.Type).MayDetachAsync(upload.TargetId.Value);

    /// <summary>
    /// The module's rule for this upload type.
    /// </summary>
    /// <remarks>
    /// Fails closed: a type with no authorizer throws rather than being treated as
    /// unrestricted, so adding one to the enum without a rule breaks every path
    /// through it instead of opening them.
    /// </remarks>
    private IUploadTargetAuthorizer AuthorizerFor(UploadType type)
    {
        var authorizer = _targetAuthorizers.FirstOrDefault(a => a.Type == type);
        if (authorizer == null)
        {
            throw new InvalidOperationException(
                $"No upload target authorizer for {type}");
        }

        return authorizer;
    }

    private static HttpException UploadNotFound() => new(HttpStatusCode.NotFound, RefusalMessage.UploadNotFound);

    private static HttpBadRequestException AudienceOnlyForPostAttachment() => new(new Dictionary<string, string>
    {
        ["visibleTo"] = RefusalMessage.AudienceOnlyForPostAttachment,
    });

    private async Task<(IEnumerable<UploadDto> Uploads, PagingInfo Paging)> GetUploadsInternal(
        UploadsQuery query, Guid? userId)
    {
        var (uploads, totalCount) = await _uploadRepository.GetPageAsync(
            new UploadFilter
            {
                UserId = userId,
                Type = query.Type,
                Status = query.Status,
            },
            skip: query.Skip,
            take: query.Take);

        // Skip is an offset; PagingResult.Create reads a 1-based entity number, as PagingData does.
        var paging = new PagingInfo(PagingResult.Create(totalCount, query.Skip + 1, query.Take));
        return (await ForCaller(uploads), paging);
    }

    /// <summary>
    /// The upload as its record goes out. Who sees a marked file is told only to a caller who may
    /// open it: the names of the addressees are as much the file's as its bytes.
    /// </summary>
    private static UploadDto MapToDto(StoredUpload upload, bool canOpen)
    {
        var addressees = canOpen ? PrivateAddresseeSnapshot.ReadList(upload.AddresseesJson) : null;
        return new UploadDto
        {
            Id = upload.Id,
            UserId = upload.UserId,
            // Only populated when the store resolved the owner (the moderation
            // list and single-upload queries); null right after DirectUpload.
            UploaderUsername = upload.OwnerUsername,
            Type = upload.Type,
            TargetId = upload.TargetId,
            OriginalFileName = upload.FileName,
            ContentType = upload.ContentType,
            SizeBytes = upload.SizeBytes,
            Status = upload.Status,
            Url = upload.Url,
            // Always: the only address a closed-prefix file has, checked on every request.
            ContentUrl = UploadContentRoute.For(upload.Id),
            CreatedUtc = upload.CreatedUtc,
            ConfirmedUtc = upload.ConfirmedUtc,
            CanOpen = canOpen,
            Recipients = addressees == null ? null : PrivateRecipients.Line(addressees.Names),
            Addressees = addressees?.Characters
                .Select(c => new UploadAddressee { Id = c.CharacterId, Name = c.CharacterName }).ToArray(),
        };
    }
}
