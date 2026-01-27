using System;
using System.Text;
using DM.Services.Authentication.Implementation.Security;
using DM.Tests.Core;
using FluentAssertions;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Authentication.Tests;

public class SecurityManagerShould : UnitTestBase
{
    private readonly Mock<ISaltFactory> saltFactory;
    private readonly Mock<IHashProvider> hashProvider;
    private readonly ISetup<IHashProvider, byte[]> computeSha256Setup;
    private readonly ISetup<IHashProvider, byte[]> computePbkdf2Setup;
    private readonly SecurityManager securityManager;

    public SecurityManagerShould()
    {
        saltFactory = Mock<ISaltFactory>();

        hashProvider = Mock<IHashProvider>();
        computeSha256Setup = hashProvider.Setup(p => p.ComputeSha256(It.IsAny<string>(), It.IsAny<string>()));
        computePbkdf2Setup = hashProvider.Setup(p => p.ComputePbkdf2(It.IsAny<string>(), It.IsAny<string>()));
        hashProvider.Setup(p => p.CurrentVersion).Returns(2);

        securityManager = new SecurityManager(saltFactory.Object, hashProvider.Object);
    }

    [Fact]
    public void EncryptPasswordForStorage()
    {
        saltFactory.Setup(f => f.Create(It.IsAny<int>())).Returns("salt");
        var expectedHash = Convert.ToBase64String(Encoding.UTF8.GetBytes("hash"));
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        computePbkdf2Setup.Returns(expectedHashBytes);

        var (actualHash, actualSalt, actualVersion) = securityManager.GeneratePassword("qwerty");
        actualHash.Should().Be(expectedHash);
        actualSalt.Should().Be("salt");
        actualVersion.Should().Be(2); // Current PBKDF2 version
        hashProvider.Verify(p => p.ComputePbkdf2("qwerty", "salt"), Times.Once);
    }

    [Fact]
    public void ConfirmPasswordEquivalency()
    {
        var base64Hash = Convert.ToBase64String(Encoding.UTF8.GetBytes("hash"));
        computeSha256Setup.Returns(Encoding.UTF8.GetBytes("hash"));

        securityManager.ComparePasswords("qwerty", "salt", base64Hash, 1).Should().BeTrue();
        hashProvider.Verify(p => p.ComputeSha256("qwerty", "salt"), Times.Once);
    }

    [Fact]
    public void ConfirmPasswordInequality()
    {
        var base64Hash = Convert.ToBase64String(Encoding.UTF8.GetBytes("hash"));
        computeSha256Setup.Returns(Encoding.UTF8.GetBytes("notHash"));

        securityManager.ComparePasswords("qwerty", "salt", base64Hash, 1).Should().BeFalse();
        hashProvider.Verify(p => p.ComputeSha256("qwerty", "salt"), Times.Once);
    }
}