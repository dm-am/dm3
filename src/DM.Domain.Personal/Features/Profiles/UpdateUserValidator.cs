using DM.Domain.Core.Content;
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

        // The extended information is the one profile field written in BBCode,
        // and it renders on the Profile surface, which declares neither [mod]
        // nor [private]. The tag is not markup there, so it hides nothing and
        // the line is shown on a page anyone may open.
        Unless(u => u.Info == null, () =>
            RuleFor(u => u.Info)
                .Must(info => !PrivateBlockMarkup.ContainsPrivateMarkup(info))
                .WithMessage(u => PrivateBlockMarkup.DescribeSurfaceRefusal(u.Info)));

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
