using System;
using System.Threading;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DM.Domain.Account.Tests.Dsl;

public abstract class UsernameChangeServiceTestBase : UnitTestBase
{
    protected readonly IValidator<CreateUsernameChangeRequest> validator = Mock<IValidator<CreateUsernameChangeRequest>>();
    protected readonly IUsernameChangeRepository repository = Mock<IUsernameChangeRepository>();
    protected readonly IUsernameHistoryRepository historyRepository = Mock<IUsernameHistoryRepository>();
    protected readonly IIdentityProvider identityProvider = Mock<IIdentityProvider>();
    protected readonly IIntentionManager intentionManager = Mock<IIntentionManager>();
    protected readonly IGuidFactory guidFactory = Mock<IGuidFactory>();
    protected readonly IDateTimeProvider dateTimeProvider = Mock<IDateTimeProvider>();
    protected readonly IUsernameChangeMailSender notificationSender = Mock<IUsernameChangeMailSender>();

    // Not the default, so an approval that still counted its own hours would show.
    protected readonly TokenConfiguration tokens = new() { UsernameChangeApprovalTokenLifetimeHours = 30 };
    private protected readonly UsernameChangeService service;

    protected UsernameChangeServiceTestBase()
    {
        validator.ValidateAsync(
                Arg.Any<ValidationContext<CreateUsernameChangeRequest>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        // A distinct value per call: the approval token and the history id are issued
        // in one and the same flow, and a single fixed guid would hide a swap of them.
        guidFactory.Create().Returns(_ => Guid.NewGuid());

        service = new UsernameChangeService(
            validator,
            repository,
            historyRepository,
            identityProvider,
            intentionManager,
            guidFactory,
            dateTimeProvider,
            notificationSender,
            Options.Create(tokens));
    }
}
