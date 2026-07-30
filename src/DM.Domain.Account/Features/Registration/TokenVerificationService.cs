using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Account.Features.Registration;

/// <inheritdoc />
internal class TokenVerificationService : ITokenVerificationService
{
    private readonly ITokenVerificationRepository _repository;

    /// <inheritdoc />
    public TokenVerificationService(
        ITokenVerificationRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Verify(Guid token)
    {
        var owner = await _repository.GetTokenOwner(token);
        if (owner == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrUsed);
        }

        return owner;
    }
}