using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Dto;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class TokenVerificationRepository : ITokenVerificationRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public TokenVerificationRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<GeneralUser?> GetTokenOwner(Guid tokenId) => _dbContext.Tokens
        .Where(t => t.TokenId == tokenId)
        .Select(t => t.User)
        .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();
}
