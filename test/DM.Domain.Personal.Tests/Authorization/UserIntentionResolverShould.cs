using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Authorization;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Authorization;

public class UserIntentionResolverShould
{
    private readonly UserIntentionResolver resolver = new();

    [Theory]
    [InlineData(UserRole.RegularUser, UserIntention.Edit, true, true)]
    [InlineData(UserRole.RegularUser, UserIntention.Edit, false, false)]
    [InlineData(UserRole.RegularUser, UserIntention.WriteMessage, false, true)]
    [InlineData(UserRole.RegularUser, UserIntention.WriteMessage, true, false)]
    [InlineData(UserRole.SeniorModerator, UserIntention.Moderate, false, true)]
    [InlineData(UserRole.Moderator, UserIntention.Moderate, false, false)]
    [InlineData(UserRole.Moderator, UserIntention.ReadModNotes, false, true)]
    [InlineData(UserRole.RegularUser, UserIntention.ReadModNotes, false, false)]
    [InlineData(UserRole.Moderator, UserIntention.CreateModNote, false, true)]
    [InlineData(UserRole.Moderator, UserIntention.ViewModeratedProfile, false, true)]
    [InlineData(UserRole.Admin, UserIntention.ViewUserIpData, false, true)]
    [InlineData(UserRole.SeniorModerator, UserIntention.ViewUserIpData, false, false)]
    public void AnswerByTheRoleAndWhoseProfileItIs(
        UserRole role, UserIntention intention, bool ownProfile, bool allowed)
    {
        var user = Create.User().WithRole(role).Please();
        var target = new GeneralUser { UserId = ownProfile ? user.UserId : Guid.NewGuid() };

        resolver.IsAllowed(user, intention, target).Should().Be(allowed);
    }
}
