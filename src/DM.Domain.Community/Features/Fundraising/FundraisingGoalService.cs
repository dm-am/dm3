using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Community.Features.Fundraising;

/// <inheritdoc />
internal class FundraisingGoalService : IFundraisingGoalService
{
    private readonly IValidator<UpdateFundraisingGoal> _updateValidator;
    private readonly IFundraisingGoalRepository _repository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public FundraisingGoalService(
        IValidator<UpdateFundraisingGoal> updateValidator,
        IFundraisingGoalRepository repository,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider)
    {
        _updateValidator = updateValidator;
        _repository = repository;
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<FundraisingGoal> GetAsync()
    {
        var goal = await _repository.Get();
        if (goal == null)
        {
            // The row is seeded in the InitialCreate migration, so this is exceptional
            throw new HttpException(HttpStatusCode.NotFound, "Fundraising goal not found");
        }

        return goal;
    }

    /// <inheritdoc />
    public async Task<FundraisingGoal> UpdateAsync(UpdateFundraisingGoal updateGoal)
    {
        await _updateValidator.ValidateAndThrowAsync(updateGoal);

        // Authorization (admin only) is enforced on the API layer via [RequireRole]
        return await _repository.Update(
            updateGoal,
            _identityProvider.Current.User.UserId,
            _dateTimeProvider.Now);
    }
}
