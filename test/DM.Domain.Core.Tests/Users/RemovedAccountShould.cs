using DM.Domain.Core.Users;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Users;

/// <summary>
/// Which names mean the profile of removed accounts, and which names no account may take.
/// </summary>
/// <remarks>
/// The two answers differ on purpose and in one direction only. A reader reaches the
/// profile by the name as he would type it; an account is refused every spelling a reader
/// would take for that name, because a live account signed "Aккаунт удален" with a Latin
/// "A" is, on the page, the removed ones.
/// </remarks>
public class RemovedAccountShould
{
    /// <summary>The "е" with two dots, spelled as its code point as the sources require.</summary>
    private const char DottedE = (char)0x0451;

    public static TheoryData<string> TheProfileName =>
    [
        RemovedAccount.Name,
        "Аккаунт Удален",
        "АККАУНТ УДАЛЕН",
        "  аккаунт удален  ",
        $"Аккаунт удал{DottedE}н",
    ];

    public static TheoryData<string> LookalikesOfIt =>
    [
        "Aккаунт удален",
        "Аккаунт удaлен",
        "AKKAYHT YДAЛEH",
        "Аккаунт  удален",
        "Аккаунт_удален",
        "Аккаунт-удален",
        "Аккаунтудален",
    ];

    public static TheoryData<string> OtherNames =>
    [
        "Аккаунт",
        "Удален",
        "Аккаунт удален 2",
        "Аккаунт удаленный",
        "Удаленный аккаунт",
        "Эльвира",
    ];

    [Theory]
    [MemberData(nameof(TheProfileName))]
    public void TakeTheNameAsAReaderTypesIt(string username)
    {
        RemovedAccount.Is(username).Should().BeTrue(
            "the case, the surrounding spaces and the dots over the letter are not part of a name");
    }

    [Theory]
    [MemberData(nameof(TheProfileName))]
    [MemberData(nameof(LookalikesOfIt))]
    public void ReserveEverySpellingAReaderWouldTakeForIt(string username)
    {
        RemovedAccount.IsReserved(username).Should().BeTrue(
            "an account named so would sign its posts the way the removed ones are signed");
    }

    /// <summary>
    /// A lookalike is refused as a name and still not taken for the profile: the address
    /// answers the name, and "Aккаунт удален" with a Latin letter is not it.
    /// </summary>
    [Theory]
    [MemberData(nameof(LookalikesOfIt))]
    public void NotOpenTheProfileForALookalike(string username)
    {
        RemovedAccount.Is(username).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(OtherNames))]
    public void LeaveEveryOtherNameAlone(string username)
    {
        RemovedAccount.Is(username).Should().BeFalse();
        RemovedAccount.IsReserved(username).Should().BeFalse(
            "the reservation is of one name, not of the words in it");
    }

    [Fact]
    public void AnswerNoForNoName()
    {
        RemovedAccount.Is(null).Should().BeFalse();
        RemovedAccount.IsReserved(null).Should().BeFalse();
        RemovedAccount.IsAmong(null).Should().BeFalse();
    }

    [Fact]
    public void FindTheNameAmongOthers()
    {
        RemovedAccount.IsAmong(["Эльвира", "аккаунт удален"]).Should().BeTrue();
        RemovedAccount.IsAmong(["Эльвира", "Aккаунт удален"]).Should().BeFalse();
    }
}
