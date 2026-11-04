using System;
using System.Linq;
using DM.Domain.Account.Features.Security;
using AwesomeAssertions;
using Xunit;
using DomainSecurityEventType = DM.Domain.Account.Features.Security.SecurityEventType;

namespace DM.Web.API.Tests.Features.Account;

/// <summary>
/// A new kind of security event has to arrive in every list that enumerates them.
/// </summary>
/// <remarks>The enum and the journal filters live apart, and adding a member to the enum compiles.</remarks>
public class SecurityJournalEventsShould
{
    [Fact]
    public void FileEverySecondFactorEventUnderTheSecondFactorFilter()
    {
        var ofTheFactor = Enum.GetValues<DomainSecurityEventType>()
            .Where(type => type.ToString().StartsWith("TwoFactor", StringComparison.Ordinal));

        SecurityEventCategories.TwoFactor.Should().Contain(ofTheFactor,
            "the question this filter answers is whether somebody has been working on " +
            "the way into the account, and an entry outside it is an entry the owner " +
            "looking for exactly that never sees");
    }
}
