using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.BusinessObjects.Users.Settings;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Uploading.BusinessProcesses.PublicImage;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Users.Updating;

/// <inheritdoc />
internal class UserUpdatingService : IUserUpdatingService
{
    private readonly IValidator<UpdateUser> _validator;
    private readonly IUserReadingService _userReadingService;
    private readonly IPublicImageService _imageService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IUserUpdatingRepository _repository;
    private readonly IGuidFactory _guidFactory;
    private readonly ICache _cache;
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public UserUpdatingService(
        IValidator<UpdateUser> validator,
        IUserReadingService userReadingService,
        IPublicImageService imageService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IUserUpdatingRepository repository,
        IGuidFactory guidFactory,
        ICache cache,
        DmDbContext dbContext)
    {
        _validator = validator;
        _userReadingService = userReadingService;
        _imageService = imageService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _guidFactory = guidFactory;
        _cache = cache;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UserDetails> Update(UpdateUser updateUser)
    {
        await _validator.ValidateAndThrowAsync(updateUser);
        var user = await _userReadingService.Get(updateUser.Login);
        _intentionManager.ThrowIfForbidden(UserIntention.Edit, user);

        #pragma warning disable CS8603 // Possible null reference return - false positive with MaybeField fluent chain
        var userUpdate = _updateBuilderFactory.Create<User>(user.UserId)
            .MaybeField(u => u.Status, updateUser.Status?.Trim())
            .MaybeField(u => u.Name, updateUser.Name?.Trim())
            .MaybeField(u => u.Location, updateUser.Location?.Trim())
            .MaybeField(u => u.Info, updateUser.Info?.Trim())
            .MaybeField(u => u.RatingDisabled, updateUser.RatingDisabled);
        #pragma warning restore CS8603

        // Handle avatar from presigned URL upload
        if (updateUser.AvatarUploadId.HasValue)
        {
            var upload = await _dbContext.Uploads
                .FirstOrDefaultAsync(u =>
                    u.UploadId == updateUser.AvatarUploadId.Value &&
                    u.UserId == user.UserId &&
                    u.Type == UploadType.UserAvatar &&
                    u.Status == UploadStatus.Confirmed &&
                    !u.IsRemoved);

            if (upload == null)
            {
                throw new HttpBadRequestException(
                    new Dictionary<string, string>
                    {
                        [nameof(updateUser.AvatarUploadId)] = "Avatar upload not found or not confirmed"
                    });
            }

            userUpdate
                .Field(u => u.AvatarUploadId, upload.UploadId);

            // Link upload to user entity
            upload.EntityId = user.UserId;

            // Mark old avatar uploads as obsolete for cleanup
            await _imageService.PrepareObsoleteForDeleting(user.UserId);
        }

        // Handle contacts replacement (if provided, replace all contacts)
        if (updateUser.Contacts != null)
        {
            var existingContacts = await _dbContext.UserContacts
                .Where(c => c.UserId == user.UserId)
                .ToListAsync();
            _dbContext.UserContacts.RemoveRange(existingContacts);

            var newContacts = updateUser.Contacts.Select((c, i) => new UserContact
            {
                UserContactId = _guidFactory.Create(),
                UserId = user.UserId,
                ContactType = c.ContactType.Trim(),
                ContactValue = c.ContactValue.Trim(),
                SortOrder = c.SortOrder > 0 ? c.SortOrder : i
            }).ToList();
            _dbContext.UserContacts.AddRange(newContacts);
        }

        #pragma warning disable CS8603 // Possible null reference return - false positive with MaybeField fluent chain
        var settingsUpdate = _updateBuilderFactory.Create<UserSettings>(user.UserId)
            .MaybeField(u => u.ColorSchema, updateUser.Settings?.ColorSchema)
            .MaybeField(u => u.MentorGreetingsMessage, updateUser.Settings?.MentorGreetingsMessage)
            .MaybeField(u => u.Paging.CommentsPerPage, updateUser.Settings?.Paging?.CommentsPerPage)
            .MaybeField(u => u.Paging.TopicsPerPage, updateUser.Settings?.Paging?.TopicsPerPage)
            .MaybeField(u => u.Paging.MessagesPerPage, updateUser.Settings?.Paging?.MessagesPerPage)
            .MaybeField(u => u.Paging.PostsPerPage, updateUser.Settings?.Paging?.PostsPerPage)
            .MaybeField(u => u.Paging.EntitiesPerPage, updateUser.Settings?.Paging?.EntitiesPerPage);
        #pragma warning restore CS8603

        await _repository.UpdateUser(userUpdate, settingsUpdate);
        await _dbContext.SaveChangesAsync();

        // Invalidate cache for both login and userId lookups
        await _cache.Invalidate($"user_details_{updateUser.Login.ToLowerInvariant()}");
        await _cache.Invalidate($"user_details_{user.UserId}");

        return await _userReadingService.GetDetails(updateUser.Login);
    }

}