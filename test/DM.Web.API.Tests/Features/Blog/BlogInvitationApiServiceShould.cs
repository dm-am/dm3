using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Testing;
using DM.Web.API.Features.Blog.Invitations;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainBlogInvitation = DM.Domain.Blog.Features.Invitations.BlogInvitation;

namespace DM.Web.API.Tests.Features.Blog;

/// <summary>
/// A blog invitation names its role in the same words as a game invitation and
/// the account's list of received invitations.
/// </summary>
/// <remarks>
/// The blog answered with the enum member's own name ("Assistant", "Reader")
/// while the game and the account area answered with the API word ("assistant",
/// "reader"), so the one screen that lists both kinds had to lower-case one of
/// them to tell them apart.
/// </remarks>
public class BlogInvitationApiServiceShould : UnitTestBase
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
