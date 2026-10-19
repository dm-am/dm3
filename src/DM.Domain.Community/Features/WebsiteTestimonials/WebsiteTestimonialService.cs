using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <inheritdoc />
internal class WebsiteTestimonialService : IWebsiteTestimonialService
{
    private readonly IValidator<CreateWebsiteTestimonial> _createValidator;
    private readonly IValidator<UpdateWebsiteTestimonial> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IWebsiteTestimonialRepository _repository;
    private readonly IUserReadRepository _userRepository;
    private readonly IEventProducer _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IIdentityProvider _identityProvider;

    public WebsiteTestimonialService(
        IValidator<CreateWebsiteTestimonial> createValidator,
        IValidator<UpdateWebsiteTestimonial> updateValidator,
        IIntentionManager intentionManager,
        IWebsiteTestimonialRepository repository,
        IUserReadRepository userRepository,
        IEventProducer producer,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
        _userRepository = userRepository;
        _producer = producer;
        _dateTimeProvider = dateTimeProvider;
        _guidFactory = guidFactory;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> CreateAsync(CreateWebsiteTestimonial createTestimonial)
    {
        await _createValidator.ValidateAndThrowAsync(createTestimonial);
        _intentionManager.ThrowIfForbidden(WebsiteTestimonialIntention.Create);

        // Signed by the participant the moderator named; an unknown name would sign
        // the wrong person or store a foreign key to nobody.
        var authorId = await _userRepository.FindUserIdAsync(createTestimonial.AuthorUsername);
        if (authorId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound,
                RefusalMessage.UserNotFoundByUsername(createTestimonial.AuthorUsername));
        }

        // One testimonial per participant: a second one is a conflict, which the form branches on.
        var existing = await _repository.GetByAuthor(authorId.Value);
        if (existing != null)
        {
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.TestimonialAlreadyExists(createTestimonial.AuthorUsername));
        }

        var entity = new CreateWebsiteTestimonialEntity
        {
            Id = _guidFactory.Create(),
            AuthorId = authorId.Value,
            CreatedUtc = _dateTimeProvider.Now.UtcDateTime,
            Text = createTestimonial.Text
        };

        WebsiteTestimonial result;
        try
        {
            result = await _repository.Create(entity);
        }
        catch (DuplicateEntityException)
        {
            // The loser of the race reads as the ordinary second attempt does.
            throw new HttpException(HttpStatusCode.Conflict,
                RefusalMessage.TestimonialAlreadyExists(createTestimonial.AuthorUsername));
        }

        await _producer.SendAsync(EventType.NewWebsiteTestimonial, result.Id);

        return result;
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> GetAsync(Guid id) =>
        await _repository.Get(id) ?? throw new HttpException(HttpStatusCode.NotFound, "Отзыв не найден");

    /// <inheritdoc />
    public async Task<(IEnumerable<WebsiteTestimonial> Testimonials, PagingResult Paging)> GetListAsync(
        WebsiteTestimonialsQuery query)
    {
        var totalCount = await _repository.Count(query);
        var pageSize = _identityProvider.Current.Settings.PageSize;
        var pagingData = new PagingData(query, pageSize, totalCount);
        var testimonials = await _repository.Get(query, pagingData);
        return (testimonials, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> UpdateAsync(UpdateWebsiteTestimonial updateTestimonial)
    {
        await _updateValidator.ValidateAndThrowAsync(updateTestimonial);
        var existing = await GetAsync(updateTestimonial.Id);
        _intentionManager.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, existing);

        var entity = new UpdateWebsiteTestimonialEntity
        {
            Id = updateTestimonial.Id,
            ModifiedUtc = _dateTimeProvider.Now.UtcDateTime,
            ModifiedByUserId = _identityProvider.Current.User.UserId,
            Text = updateTestimonial.Text
        };

        return await _repository.Update(entity);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        var testimonial = await GetAsync(id);
        _intentionManager.ThrowIfForbidden(WebsiteTestimonialIntention.Delete, testimonial);
        await _repository.Delete(id, _identityProvider.Current.User.UserId);
    }
}
