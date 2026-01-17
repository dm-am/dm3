using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Authentication.Dto;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Account.EmailChange;

/// <inheritdoc />
internal class EmailChangeRepository : IEmailChangeRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public EmailChangeRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<AuthenticatedUser> FindUser(string login) => _dbContext.Users
        .Where(u => u.Login.ToLower() == login.ToLower())
        .ProjectTo<AuthenticatedUser>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public Task Update(IUpdateBuilder<User> updateUser, Token token)
    {
        updateUser.AttachTo(_dbContext);
        _dbContext.Tokens.Add(token);
        return _dbContext.SaveChangesAsync();
    }
}