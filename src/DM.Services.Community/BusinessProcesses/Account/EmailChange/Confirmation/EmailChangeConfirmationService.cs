using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Community.Configuration;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using Microsoft.Extensions.Options;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange.Confirmation;

/// <inheritdoc />
internal class EmailChangeConfirmationService : IEmailChangeConfirmationService
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEmailChangeConfirmationRepository _repository;
    private readonly TokenConfiguration _tokenConfig;

    /// <inheritdoc />
    public EmailChangeConfirmationService(
        IDateTimeProvider dateTimeProvider,
        IEmailChangeConfirmationRepository repository,
        IOptions<TokenConfiguration> tokenOptions)
    {
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _tokenConfig = tokenOptions.Value;
    }

    /// <inheritdoc />
    public async Task Confirm(Guid tokenId)
    {
        var foundTokenId = await _repository.FindEmailChangeToken(
            tokenId,
            _dateTimeProvider.Now - TimeSpan.FromHours(_tokenConfig.EmailChangeTokenLifetimeHours));

        if (!foundTokenId.HasValue)
        {
            throw new HttpException(HttpStatusCode.Gone,
                "Email change confirmation token is invalid or expired");
        }

        await _repository.MarkTokenUsed(foundTokenId.Value);
    }
}
