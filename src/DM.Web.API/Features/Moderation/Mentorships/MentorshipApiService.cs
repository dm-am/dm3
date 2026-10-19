using System;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Mentorships;

namespace DM.Web.API.Features.Moderation.Mentorships;

public class MentorshipApiService
{
    private readonly IMentorshipService _mentorshipService;

    public MentorshipApiService(IMentorshipService mentorshipService)
    {
        _mentorshipService = mentorshipService;
    }

    public Task AssignGameMentor(Guid gameId) =>
        _mentorshipService.AssignGameMentor(gameId);

    public Task RemoveGameMentor(Guid gameId) =>
        _mentorshipService.RemoveGameMentor(gameId);

    public Task AssignBlogMentor(Guid blogId) =>
        _mentorshipService.AssignBlogMentor(blogId);

    public Task RemoveBlogMentor(Guid blogId) =>
        _mentorshipService.RemoveBlogMentor(blogId);
}
