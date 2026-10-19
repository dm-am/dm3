using System;
using DM.Domain.Community.Features.Awards;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Awards;

/// <summary>
/// The year of a contest series runs from the opening of the site to a year past the
/// current one, on creation and on change alike.
/// </summary>
public class ContestSeriesYearShould
{
    private readonly IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public ContestSeriesYearShould() =>
        dateTimeProvider.Now.Returns(new DateTimeOffset(2026, 12, 20, 12, 0, 0, TimeSpan.Zero));

    private static CreateContestSeries Create(int year) =>
        new() { ContestType = ContestType.Literary, Number = 1, Year = year };

    private static UpdateContestSeries Update(int year) => new() { Id = Guid.NewGuid(), Year = year };

    [Theory]
    [InlineData(ContestSeries.MinYear)]
    [InlineData(2026)]
    [InlineData(2027)]
    public void AcceptAYearInRange(int year)
    {
        new CreateContestSeriesValidator(dateTimeProvider).TestValidate(Create(year))
            .ShouldNotHaveValidationErrorFor(s => s.Year);
        new UpdateContestSeriesValidator(dateTimeProvider).TestValidate(Update(year))
            .ShouldNotHaveValidationErrorFor(s => s.Year);
    }

    [Theory]
    [InlineData(ContestSeries.MinYear - 1)]
    [InlineData(2028)]
    public void RejectAYearOutOfRange(int year)
    {
        new CreateContestSeriesValidator(dateTimeProvider).TestValidate(Create(year))
            .ShouldHaveValidationErrorFor(s => s.Year);
        new UpdateContestSeriesValidator(dateTimeProvider).TestValidate(Update(year))
            .ShouldHaveValidationErrorFor(s => s.Year);
    }

    [Fact]
    public void ReadTheCurrentYearOnEveryCheck()
    {
        var validator = new CreateContestSeriesValidator(dateTimeProvider);
        validator.TestValidate(Create(2028)).ShouldHaveValidationErrorFor(s => s.Year);

        dateTimeProvider.Now.Returns(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        validator.TestValidate(Create(2028)).ShouldNotHaveValidationErrorFor(s => s.Year);
    }
}
