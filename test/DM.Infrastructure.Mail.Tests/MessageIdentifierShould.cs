using System;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

/// <summary>
/// A Message-Id a relay will not replace, and that names the send it belongs to.
/// </summary>
public class MessageIdentifierShould
{
    private static readonly Guid Token = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void SpellTheIdentifierTheWayTheStandardDoes()
    {
        MessageIdentifier.Build(Token, "noreply@dm.am")
            .Should().Be("11111111222233334444555555555555@dm.am",
                "a msg-id is id-left, an at sign and id-right, and a bare guid is neither half");
    }

    [Fact]
    public void CarryTheCorrelationTokenOfTheSend()
    {
        MessageIdentifier.Build(Token, "noreply@dm.am")
            .Should().StartWith(Token.ToString("N"),
                "the log of the send names the same value, and matching them is the point");
    }

    [Fact]
    public void FallBackWhenTheSenderAddressCarriesNoDomain()
    {
        MessageIdentifier.Build(Token, "noreply")
            .Should().EndWith("@localhost",
                "a header ending in an at sign is the malformed value this exists to avoid");

        MessageIdentifier.Build(Token, null)
            .Should().EndWith("@localhost");
    }
}
