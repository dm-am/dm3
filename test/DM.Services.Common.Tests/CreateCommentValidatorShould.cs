using System;
using System.Threading.Tasks;
using DM.Services.Common.Dto;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace DM.Services.Common.Tests;

public class CreateCommentValidatorShould
{
    private readonly CreateCommentValidator validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData(null!)]
    public async Task ThrowValidationExceptionWhenTextIsEmpty(string? text)
    {
        var err = await validator.Awaiting(v => v.ValidateAndThrowAsync(new CreateComment { Text = text! }))
            .Should().ThrowAsync<ValidationException>();
        err.And.Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Fact]
    public async Task ThrowValidationExceptionWhenTopicIdIsEmpty()
    {
        var err = await validator.Awaiting(v => v.ValidateAndThrowAsync(new CreateComment { Text = "something" }))
            .Should().ThrowAsync<ValidationException>();
        err.And.Errors.Should().ContainSingle(e => e.PropertyName == "EntityId");
    }

    [Fact]
    public async Task NotThrowWhenAllOk()
    {
        await validator.Awaiting(v => v.ValidateAndThrowAsync(new CreateComment
            {
                Text = "something",
                EntityId = Guid.NewGuid()
            }))
            .Should().NotThrowAsync();
    }
}