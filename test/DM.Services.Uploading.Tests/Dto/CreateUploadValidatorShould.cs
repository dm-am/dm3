using DM.Services.Core.Exceptions;
using DM.Services.Uploading.Dto;
using FluentAssertions;
using Xunit;

namespace DM.Services.Uploading.Tests.Dto;

public class CreateUploadValidatorShould
{
    private readonly CreateUploadValidator validator;

    public CreateUploadValidatorShould()
    {
        validator = new CreateUploadValidator();
    }

    [Fact]
    public void PassValidationWhenFileNameIsProvided()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "test-image.jpg"
        };

        // Act
        var result = validator.Validate(createUpload);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FailValidationWhenFileNameIsNull()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = null!
        };

        // Act
        var result = validator.Validate(createUpload);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyName.Should().Be(nameof(CreateUpload.FileName));
        result.Errors[0].ErrorMessage.Should().Be(ValidationError.Empty);
    }

    [Fact]
    public void FailValidationWhenFileNameIsEmpty()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = string.Empty
        };

        // Act
        var result = validator.Validate(createUpload);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyName.Should().Be(nameof(CreateUpload.FileName));
        result.Errors[0].ErrorMessage.Should().Be(ValidationError.Empty);
    }

    [Fact]
    public void FailValidationWhenFileNameIsWhitespace()
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = "   "
        };

        // Act
        var result = validator.Validate(createUpload);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().Be(ValidationError.Empty);
    }

    [Theory]
    [InlineData("image.jpg")]
    [InlineData("document.pdf")]
    [InlineData("my file with spaces.png")]
    [InlineData("файл-на-русском.jpg")]
    [InlineData("文件.gif")]
    public void PassValidationForVariousValidFileNames(string fileName)
    {
        // Arrange
        var createUpload = new CreateUpload
        {
            FileName = fileName
        };

        // Act
        var result = validator.Validate(createUpload);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
