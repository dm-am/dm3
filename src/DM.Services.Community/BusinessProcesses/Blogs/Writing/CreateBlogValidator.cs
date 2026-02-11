using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

/// <inheritdoc />
internal class CreateBlogValidator : AbstractValidator<CreateBlog>
{
    /// <inheritdoc />
    public CreateBlogValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(200).WithMessage(ValidationError.Long);
    }
}
