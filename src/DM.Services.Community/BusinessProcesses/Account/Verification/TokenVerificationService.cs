using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Account.Verification;

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
            throw new HttpException(HttpStatusCode.Gone, "Token is invalid");
        }

        return owner;
    }
}