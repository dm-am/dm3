using AwesomeAssertions;
using Xunit;
using DM.Workers.Mail.Sending;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// The address in the log is enough to tell two letters apart and not enough to write
/// to.
/// </summary>
/// <remarks>It runs before anything has parsed the address, so it answers whatever it is handed rather than throw.</remarks>
public class AddressObfuscationExtensionsShould
{
    /// <summary>
    /// Every part of whatever it is given, cropped or answered with itself.
    /// </summary>
    /// <param name="address">What the letter carried in its address field.</param>
    /// <param name="expected">What the log line is allowed to say about it.</param>
    [Theory]
    [InlineData("reader@example.com", "re..r@ex..m")]
    [InlineData("abcd@example.com", "ab..d@ex..m")]
    // A part with nothing left to hide is answered with itself.
    [InlineData("a@example.com", "a@ex..m")]
    // An address with no local part at all.
    [InlineData("@example.com", "@ex..m")]
    [InlineData("", "")]
    // Not an address: whatever is in the field is what gets logged.
    [InlineData("reader", "re..r")]
    public void CropTheMiddleOutOfEveryPartOfWhateverItIsGiven(string address, string expected) =>
        address.Obfuscate().Should().Be(expected,
            "the line is written before the address is parsed, so every value the queue can " +
            "carry has to come back out of here");

    [Fact]
    public void LeaveNeitherTheMailboxNorTheDomainReadable()
    {
        const string address = "constantine.reader@mail.example.com";

        var obfuscated = address.Obfuscate();

        obfuscated.Should().NotContain("reader",
            "the mailbox is what the log would be read for, and half of one is still it");
        obfuscated.Should().NotContain("mail.example",
            "the domain says which relay a reader is behind, which is worth as much to whoever " +
            "reads the log as the mailbox");
    }
}
