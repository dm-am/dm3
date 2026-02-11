using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Users.Reading;

namespace DM.Services.Community.BusinessProcesses.Users.LoginHistory;

/// <inheritdoc />
internal class LoginHistoryService : ILoginHistoryService
{
    private readonly ILoginHistoryRepository _repository;
    private readonly IUserReadingService _userReadingService;

    public LoginHistoryService(
        ILoginHistoryRepository repository,
        IUserReadingService userReadingService)
    {
        _repository = repository;
        _userReadingService = userReadingService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LoginHistoryEntry>> GetByLogin(string login)
    {
        var user = await _userReadingService.Get(login);
        return await _repository.GetByUserId(user.UserId);
    }
}
