using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Blog.Invitations;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainBlogInvitation = DM.Domain.Blog.Features.Invitations.BlogInvitation;

namespace DM.Web.API.Tests.Features.Blog;

// The account's list shows both kinds, so both answer with the API word.
public class BlogInvitationApiServiceShould
{
    private readonly IBlogInvitationService _invitationService = Substitute.For<IBlogInvitationService>();

    [Theory]
    [InlineData(BlogRole.Assistant, "assistant")]
    [InlineData(BlogRole.Reader, "reader")]
    public async Task NameTheRoleOfAPendingInvitationInTheApiWord(BlogRole role, string expected)
    {
        var blogId = Guid.NewGuid();
        _invitationService.GetPendingInvitations(blogId).Returns([Invitation(blogId, role)]);

        var invitations = await new BlogInvitationApiService(_invitationService).GetBlogInvitations(blogId);

        invitations.Single().Type.Should().Be(expected);
    }

    [Fact]
    public async Task NameTheRoleOfANewInvitationInTheApiWord()
    {
        var blogId = Guid.NewGuid();
        _invitationService.InviteAssistant(blogId, "reader")
            .Returns(Invitation(blogId, BlogRole.Assistant));

        var invitation = await new BlogInvitationApiService(_invitationService)
            .CreateAssistantInvitation(blogId, "reader");

        invitation.Type.Should().Be("assistant");
    }

    [Fact]
    public async Task NameTheInviteeWithTheRoleAndTheNewbieMark()
    {
        var blogId = Guid.NewGuid();
        var invitation = Invitation(blogId, BlogRole.Assistant);
        invitation.InvitedUser.Role = UserRole.Moderator;
        invitation.InvitedUser.QuantityRating = 500;
        _invitationService.GetPendingInvitations(blogId).Returns([invitation]);

        var invitee = (await new BlogInvitationApiService(_invitationService).GetBlogInvitations(blogId))
            .Single().InvitedUser;

        invitee.Role.Should().Be(UserRole.Moderator);
        invitee.IsNewbie.Should().BeFalse();
    }

    private static DomainBlogInvitation Invitation(Guid blogId, BlogRole role) => new()
    {
        TokenId = Guid.NewGuid(),
        BlogId = blogId,
        BlogTitle = "Блог",
        InvitedUser = new GeneralUser { UserId = Guid.NewGuid(), Username = "reader" },
        InvitedBy = new GeneralUser { UserId = Guid.NewGuid(), Username = "author" },
        TargetRole = role,
        CreatedUtc = DateTimeOffset.UnixEpoch,
        ExpiresUtc = DateTimeOffset.UnixEpoch.AddDays(7)
    };
}
