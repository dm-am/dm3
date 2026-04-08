using System;
using System.IO;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Storage;

public class CreateUploadValidatorShould : UnitTestBase
{
    private readonly CreateUploadValidator validator;

    public CreateUploadValidatorShould()
    {
        validator = new CreateUploadValidator();
    }

    [Fact]
    public void PassForValidUpload()
    {
        var upload = new CreateUpload
        {
            FileName = "test.jpg",
            ContentType = "image/jpeg",
            StreamAccessor = () => new MemoryStream(),
            EntityId = Guid.NewGuid()
        };

        var result = validator.TestValidate(upload);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenFileNameIsEmpty()
    {
        var upload = new CreateUpload
        {
            FileName = "",
            ContentType = "image/jpeg",
            StreamAccessor = () => new MemoryStream(),
            EntityId = Guid.NewGuid()
        };

        var result = validator.TestValidate(upload);
        result.ShouldHaveValidationErrorFor(u => u.FileName)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenFileNameIsNull()
    {
        var upload = new CreateUpload
        {
            FileName = null!,
            ContentType = "image/jpeg",
            StreamAccessor = () => new MemoryStream(),
            EntityId = Guid.NewGuid()
        };

        var result = validator.TestValidate(upload);
        result.ShouldHaveValidationErrorFor(u => u.FileName);
    }

    [Fact]
    public void FailWhenFileNameIsWhitespace()
    {
        var upload = new CreateUpload
        {
            FileName = "   ",
            ContentType = "image/jpeg",
            StreamAccessor = () => new MemoryStream(),
            EntityId = Guid.NewGuid()
        };

        var result = validator.TestValidate(upload);
        result.ShouldHaveValidationErrorFor(u => u.FileName);
    }

    [Fact]
    public void PassWithMinimalValidInput()
    {
        var upload = new CreateUpload
        {
            FileName = "a",
            ContentType = "text/plain",
            StreamAccessor = () => new MemoryStream()
        };

        var result = validator.TestValidate(upload);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
