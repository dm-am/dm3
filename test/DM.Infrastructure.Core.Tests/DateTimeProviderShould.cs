using System;
using DM.Infrastructure.Core;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class DateTimeProviderShould
{
    [Fact]
    public void ProvideCurrentMomentUtcDate()
    {
        var actual = new DateTimeProvider().Now;
        actual.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}