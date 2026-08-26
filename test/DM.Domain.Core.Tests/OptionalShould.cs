using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The difference between "the caller did not send this field" and "the caller
/// sent nothing for it".
/// </summary>
/// <remarks>
/// Every PATCH on the site is read through this: a null Optional means the field
/// was absent from the body and must be left alone, and an Optional holding null
/// means the caller asked for the value to be cleared. Collapse the two and a
/// partial update silently wipes whatever it did not mention.
/// </remarks>
public class OptionalShould
{
    [Fact]
    public void ReportNoChangeWhenTheFieldWasNotSent()
    {
        Optional<int> absent = null!;

        absent.HasChanged(7).Should().BeFalse(
            "an absent field is an instruction to leave the stored value alone");
    }

    [Fact]
    public void ReportAChangeWhenTheFieldWasSentEmptyOverAValue() =>
        Optional<int>.WithValue(null).HasChanged(7).Should().BeTrue(
            "sending the field with no value is asking for the value to be cleared");

    [Fact]
    public void ReportNoChangeWhenTheFieldWasSentEmptyOverNothing() =>
        Optional<int>.WithValue(null).HasChanged(null).Should().BeFalse();

    [Fact]
    public void ReportNoChangeWhenTheSentValueIsTheStoredOne() =>
        Optional<int>.WithValue(7).HasChanged(7).Should().BeFalse();

    [Fact]
    public void ReportAChangeWhenTheSentValueDiffers() =>
        Optional<int>.WithValue(8).HasChanged(7).Should().BeTrue();

    [Fact]
    public void ReportAChangeWhenAValueArrivesOverNothing() =>
        Optional<int>.WithValue(7).HasChanged(null).Should().BeTrue();

    [Fact]
    public void KeepWhatItWasGiven()
    {
        Optional<bool>.WithValue(true).Value.Should().BeTrue();
        Optional<bool>.WithValue(null).Value.Should().BeNull();
    }
}
