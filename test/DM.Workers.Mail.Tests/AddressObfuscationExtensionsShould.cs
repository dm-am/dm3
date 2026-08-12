using FluentAssertions;
using Xunit;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// The address in the log is enough to tell two letters apart and not enough to write
/// to.
/// </summary>
/// <remarks>
/// It runs on the way into the line the worker writes per letter, which is before
/// anything has parsed the address - so whatever it is handed it has to answer rather
/// than throw. What an address like "@host" hands it is an empty part, and it read the
/// last character of one: the letter then failed on its own log line, was retried for
/// a minute and dead lettered under an exception naming a Substring.
/// </remarks>
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
    // A part with nothing left to hide is answered with itself: "a..a" was longer than
    // what it was given and repeated the one character it was hiding.
    [InlineData("a@example.com", "a@ex..m")]
    // An address with no local part at all, which is the one that used to throw.
    [InlineData("@example.com", "@ex..m")]
    [InlineData("", "")]
    // Not an address, which this cannot rule out either: whatever is in the field is
    // what gets logged.
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
