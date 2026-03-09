using System;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Mentorships;

namespace DM.Web.API.Features.Moderation.Mentorships;

/// <inheritdoc />
internal class MentorshipApiService : IMentorshipApiService
{
    private readonly IMentorshipService _mentorshipService;

    public MentorshipApiService(IMentorshipService mentorshipService)
    {
        _mentorshipService = mentorshipService;
    }

    /// <inheritdoc />
    public Task AssignGameMentor(Guid gameId) =>
        _mentorshipService.AssignGameMentor(gameId);

    /// <inheritdoc />
    public Task RemoveGameMentor(Guid gameId) =>
        _mentorshipService.RemoveGameMentor(gameId);

    /// <inheritdoc />
    public Task AssignBlogMentor(Guid blogId) =>
        _mentorshipService.AssignBlogMentor(blogId);

    /// <inheritdoc />
    public Task RemoveBlogMentor(Guid blogId) =>
        _mentorshipService.RemoveBlogMentor(blogId);
}
