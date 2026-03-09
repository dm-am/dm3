using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Moderation.Features.Mentorships;

/// <inheritdoc />
internal class MentorshipService : IMentorshipService
{
    private readonly IMentorshipRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    public MentorshipService(
        IMentorshipRepository repository,
        IIdentityProvider identityProvider)
    {
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task AssignGameMentor(Guid gameId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.GameExists(gameId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Game not found");
        }

        var currentMentor = await _repository.GetGameMentorId(gameId, ct);
        if (currentMentor.HasValue)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Game already has a mentor assigned");
        }

        var userId = _identityProvider.Current.User.UserId;
        await _repository.SetGameMentor(gameId, userId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveGameMentor(Guid gameId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.GameExists(gameId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Game not found");
        }

        var userId = _identityProvider.Current.User.UserId;
        var currentMentor = await _repository.GetGameMentorId(gameId, ct);

        if (!currentMentor.HasValue || currentMentor.Value != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are not the mentor of this game");
        }

        await _repository.SetGameMentor(gameId, null, ct);
    }

    /// <inheritdoc />
    public async Task AssignBlogMentor(Guid blogId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.BlogExists(blogId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Blog not found");
        }

        var currentMentor = await _repository.GetBlogMentorId(blogId, ct);
        if (currentMentor.HasValue)
        {
            throw new HttpException(HttpStatusCode.Conflict, "Blog already has a mentor assigned");
        }

        var userId = _identityProvider.Current.User.UserId;
        await _repository.SetBlogMentor(blogId, userId, ct);
    }

    /// <inheritdoc />
    public async Task RemoveBlogMentor(Guid blogId, CancellationToken ct = default)
    {
        EnsureMentorRole();

        if (!await _repository.BlogExists(blogId, ct))
        {
            throw new HttpException(HttpStatusCode.NotFound, "Blog not found");
        }

        var userId = _identityProvider.Current.User.UserId;
        var currentMentor = await _repository.GetBlogMentorId(blogId, ct);

        if (!currentMentor.HasValue || currentMentor.Value != userId)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are not the mentor of this blog");
        }

        await _repository.SetBlogMentor(blogId, null, ct);
    }

    private void EnsureMentorRole()
    {
        var userRole = _identityProvider.Current.User.Role;
        if (userRole < UserRole.Mentor)
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Mentor role or higher is required");
        }
    }
}
