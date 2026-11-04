using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Core.Authorization;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Authorization;

/// <summary>
/// A refusal names the intention that was refused: its message is the title of the response.
/// </summary>
public class IntentionManagerShould
{
    private static IntentionManager Manager(params IIntentionResolver[] resolvers) =>
        new(new Subject(Create.User().WithRole(UserRole.RegularUser).Please()), resolvers,
            NullLogger<IntentionManager>.Instance);

    [Fact]
    public void NameTheRefusedIntention()
    {
        var act = () => Manager().ThrowIfForbidden(CommentIntention.Delete);

        act.Should().Throw<IntentionManagerException>().WithMessage("* perform Delete action");
    }

    [Fact]
    public void NameTheRefusedIntentionOnATarget()
    {
        var someoneElses = new Comment { Author = new GeneralUser { UserId = Guid.NewGuid() } };

        var act = () => Manager(new CommentIntentionResolver()).ThrowIfForbidden(CommentIntention.Delete, someoneElses);

        act.Should().Throw<IntentionManagerException>().WithMessage("* perform Delete action on Comment*");
    }

    private sealed class Subject(IAuthorizationSubject subject) : IAuthorizationContextProvider
    {
        public IAuthorizationSubject CurrentSubject => subject;
    }
}
