using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Users.LoginHistory;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <inheritdoc />
internal class LoginChangeService : ILoginChangeService
{
    private readonly IValidator<CreateLoginChangeRequest> _validator;
    private readonly ILoginChangeRepository _repository;
    private readonly ILoginHistoryRepository _loginHistoryRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DmDbContext _dbContext;

    public LoginChangeService(
        IValidator<CreateLoginChangeRequest> validator,
        ILoginChangeRepository repository,
        ILoginHistoryRepository loginHistoryRepository,
        IRegistrationRepository registrationRepository,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        DmDbContext dbContext)
    {
        _validator = validator;
        _repository = repository;
        _loginHistoryRepository = loginHistoryRepository;
        _registrationRepository = registrationRepository;
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequestEntry> Create(CreateLoginChangeRequest request)
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
            throw new HttpException(HttpStatusCode.Unauthorized, "Authentication required");

        await _validator.ValidateAndThrowAsync(request);

        // Check no pending request exists
        var existing = await _repository.GetPendingByUserId(currentUser.UserId);
        if (existing != null)
            throw new HttpException(HttpStatusCode.Conflict, "A pending login change request already exists");

        var now = _dateTimeProvider.Now;
        var entity = new LoginChangeRequest
        {
            RequestId = Guid.NewGuid(),
            UserId = currentUser.UserId,
            RequestedLogin = request.RequestedLogin,
            Reason = request.Reason,
            Status = LoginChangeRequestStatus.Pending,
            CreatedUtc = now
        };

        await _repository.Add(entity);

        return new LoginChangeRequestEntry
        {
            RequestId = entity.RequestId,
            CurrentLogin = currentUser.Login,
            UserId = currentUser.UserId,
            RequestedLogin = entity.RequestedLogin,
            Reason = entity.Reason,
            Status = entity.Status,
            CreatedUtc = entity.CreatedUtc
        };
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequestEntry?> GetCurrentUserRequest()
    {
        var currentUser = _identityProvider.Current.User;
        if (!currentUser.IsAuthenticated)
            throw new HttpException(HttpStatusCode.Unauthorized, "Authentication required");

        var entity = await _repository.GetLatestByUserId(currentUser.UserId);
        if (entity == null) return null;

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LoginChangeRequestEntry>> GetPendingRequests()
    {
        return await _repository.GetPendingRequests();
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequestEntry> GetById(Guid requestId)
    {
        var entity = await _repository.GetById(requestId);
        if (entity == null)
            throw new HttpException(HttpStatusCode.NotFound, "Login change request not found");

        return MapToEntry(entity);
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequestEntry> Resolve(ResolveLoginChangeRequest resolve)
    {
        var currentUser = _identityProvider.Current.User;
        var entity = await _repository.GetById(resolve.RequestId);
        if (entity == null)
            throw new HttpException(HttpStatusCode.NotFound, "Login change request not found");

        if (entity.Status != LoginChangeRequestStatus.Pending)
            throw new HttpException(HttpStatusCode.Conflict, "Request is already resolved");

        var now = _dateTimeProvider.Now;

        if (resolve.Status == LoginChangeRequestStatus.Approved)
        {
            // Re-validate login availability
            var loginFree = await _registrationRepository.LoginFree(entity.RequestedLogin, default);
            var loginReserved = await _loginHistoryRepository.IsLoginReserved(entity.RequestedLogin);
            if (!loginFree || loginReserved)
                throw new HttpException(HttpStatusCode.Conflict, "Requested login is no longer available");

            // Atomic: record history + update login + update request
            var historyEntry = new DataAccess.BusinessObjects.Users.LoginHistory
            {
                LoginHistoryId = Guid.NewGuid(),
                UserId = entity.UserId,
                OldLogin = entity.User.Login,
                NewLogin = entity.RequestedLogin,
                ChangedUtc = now,
                ApprovedByUserId = currentUser.UserId
            };
            _dbContext.LoginHistories.Add(historyEntry);

            entity.User.Login = entity.RequestedLogin;
        }

        entity.Status = resolve.Status;
        entity.ResolvedUtc = now;
        entity.ResolvedByUserId = currentUser.UserId;
        entity.ResolverComment = resolve.Comment;

        await _repository.SaveChanges();

        return MapToEntry(entity);
    }

    private static LoginChangeRequestEntry MapToEntry(LoginChangeRequest entity) => new()
    {
        RequestId = entity.RequestId,
        CurrentLogin = entity.User.Login,
        UserId = entity.UserId,
        RequestedLogin = entity.RequestedLogin,
        Reason = entity.Reason,
        Status = entity.Status,
        CreatedUtc = entity.CreatedUtc,
        ResolvedUtc = entity.ResolvedUtc,
        ResolvedByLogin = entity.ResolvedBy?.Login,
        ResolverComment = entity.ResolverComment
    };
}
