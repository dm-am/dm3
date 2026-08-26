using System;
using System.Linq;
using DM.Domain.Account.Features.Security;
using DM.Testing;
using DM.Web.API.Features.Account.Security;
using AwesomeAssertions;
using Xunit;
using DomainSecurityEventType = DM.Domain.Account.Features.Security.SecurityEventType;
using WireSecurityEventType = DM.Web.API.Features.Account.Security.SecurityEventType;

namespace DM.Web.API.Tests.Features.Account;

/// <summary>
/// A new kind of security event has to arrive in every list that enumerates them.
/// </summary>
/// <remarks>
/// The lists live in separate assemblies: the domain enum the writers name, the
/// wire enum the client reads, and the filters the journal screen offers. Adding
/// a member to one and not the others compiles: the reader gets a number with no
/// name, or an entry that never appears under the filter it belongs to.
///
/// It is also where a borrowed type gets caught. A refused request written as a
/// scheduled removal and a mistyped setup code written as a failed sign-in both
/// reached the owner as something that had not happened - which is worse than no
/// entry, because the owner of the account believes it.
/// </remarks>
public class SecurityJournalEventsShould : UnitTestBase
{
    [Fact]
    public void SpellTheWireEnumExactlyAsTheDomainOne()
    {
        var wire = Enum.GetValues<WireSecurityEventType>()
            .ToDictionary(value => value.ToString(), value => (int)value);
        var domain = Enum.GetValues<DomainSecurityEventType>()
            .ToDictionary(value => value.ToString(), value => (int)value);

        wire.Should().BeEquivalentTo(domain,
            "the answer carries the number, so a member missing on this side ships a " +
            "number the client cannot name, and a number that differs names the wrong " +
            "event entirely");
    }

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
