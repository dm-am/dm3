using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using FluentValidation;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class CreateUploadValidator : AbstractValidator<CreateUpload>
{
    /// <inheritdoc />
    public CreateUploadValidator()
    {
        RuleFor(u => u.FileName)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
