using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Logging;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class LoginRecordService : ILoginRecordService
{
    /// <summary>Width of the stored user agent.</summary>
    private const int UserAgentLimit = 500;

    private readonly ILoginRecordRepository _repository;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<LoginRecordService> _logger;

    /// <inheritdoc />
    public LoginRecordService(
        ILoginRecordRepository repository,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<LoginRecordService> logger)
    {
        _repository = repository;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAttempt(
        Guid? userId,
        string identifier,
        string ipAddress,
        string? userAgent,
        bool isSuccessful)
    {
        try
        {
            // A failed attempt has no identity yet, so the account it was made
            // against is resolved from what was typed. An attempt against nobody is
            // not recorded: there is no history for it to be part of.
            var subject = userId ?? await _repository.TryResolveUserId(identifier);
            if (!subject.HasValue)
            {
                return;
            }

            await _repository.Record(new UserLoginRecord
            {
                UserLoginRecordId = _guidFactory.Create(),
                UserId = subject.Value,
                IpAddress = ipAddress,
                UserAgent = userAgent is { Length: > UserAgentLimit }
                    ? userAgent[..UserAgentLimit]
                    : userAgent,
                LoginUtc = _dateTimeProvider.Now,
                IsSuccessful = isSuccessful
            });
        }
        catch (Exception ex)
        {
            // Recording a sign-in never fails the sign-in. The address stays out of
            // the message: it identifies a person and the log store has no
            // retention. The trace id and the security audit log carry the rest.
            _logger.LogWarning(ex, "Failed to record login attempt");
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UserLoginRecord>> GetHistory(Guid userId, PagingQuery query) =>
        _repository.GetLoginHistory(userId, query.Skip, query.Take);

    /// <inheritdoc />
    public Task<int> CountHistory(Guid userId) => _repository.CountLoginHistory(userId);

    /// <inheritdoc />
    public Task<IReadOnlyList<UserIpInfo>> GetIpAddresses(Guid userId) =>
        _repository.GetUserIps(userId);

    /// <inheritdoc />
    public Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId) =>
        _repository.GetLinkedProfiles(userId);
}
