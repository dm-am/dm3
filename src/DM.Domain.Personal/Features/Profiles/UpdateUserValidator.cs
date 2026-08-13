using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Dto;
using FluentValidation;

namespace DM.Domain.Personal.Features.Profiles;

/// <inheritdoc />
internal class UpdateUserValidator : AbstractValidator<UpdateUser>
{
    /// <inheritdoc />
    public UpdateUserValidator()
    {
        Unless(u => u.Status == null, () =>
            RuleFor(u => u.Status)
                .MaximumLength(200).WithMessage(ValidationError.Long));

        Unless(u => u.Name == null, () =>
            RuleFor(u => u.Name)
                .MaximumLength(100).WithMessage(ValidationError.Long));

        Unless(u => u.Location == null, () =>
            RuleFor(u => u.Location)
                .MaximumLength(100).WithMessage(ValidationError.Long));

        Unless(u => u.Contacts == null, () =>
        {
            RuleFor(u => u.Contacts)
                .Must(c => c.Count <= 10).WithMessage(ValidationError.TooMany);
            RuleForEach(u => u.Contacts).ChildRules(contact =>
            {
                contact.RuleFor(c => c.ContactType)
                    .NotEmpty().WithMessage(ValidationError.Empty)
                    .MaximumLength(50).WithMessage(ValidationError.Long);
                contact.RuleFor(c => c.ContactValue)
                    .NotEmpty().WithMessage(ValidationError.Empty)
                    .MaximumLength(200).WithMessage(ValidationError.Long);
            });
        });

        Unless(u => u.Settings == null, () =>
        {
            Unless(u => u.Settings.Paging == null, () =>
            {
                RuleFor(u => u.Settings.Paging.CommentsPerPage)
                    .Must(PagingPolicy.Allows).WithMessage(ValidationError.Invalid);

                RuleFor(u => u.Settings.Paging.MessagesPerPage)
                    .Must(PagingPolicy.Allows).WithMessage(ValidationError.Invalid);

                RuleFor(u => u.Settings.Paging.PostsPerPage)
                    .Must(PagingPolicy.Allows).WithMessage(ValidationError.Invalid);

                RuleFor(u => u.Settings.Paging.TopicsPerPage)
                    .Must(PagingPolicy.Allows).WithMessage(ValidationError.Invalid);

                RuleFor(u => u.Settings.Paging.EntitiesPerPage)
                    .Must(PagingPolicy.Allows).WithMessage(ValidationError.Invalid);
            });
        });
    }
}
