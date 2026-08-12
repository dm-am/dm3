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
using FluentValidation;

namespace DM.Domain.Community.Features.WebsiteTestimonials;

/// <inheritdoc />
internal class WebsiteTestimonialService : IWebsiteTestimonialService
{
    private readonly IValidator<CreateWebsiteTestimonial> _createValidator;
    private readonly IValidator<UpdateWebsiteTestimonial> _updateValidator;
    private readonly IIntentionManager _intentionManager;
    private readonly IWebsiteTestimonialRepository _repository;
    private readonly IEventProducer _producer;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public WebsiteTestimonialService(
        IValidator<CreateWebsiteTestimonial> createValidator,
        IValidator<UpdateWebsiteTestimonial> updateValidator,
        IIntentionManager intentionManager,
        IWebsiteTestimonialRepository repository,
        IEventProducer producer,
        IDateTimeProvider dateTimeProvider,
        IGuidFactory guidFactory,
        IIdentityProvider identityProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _intentionManager = intentionManager;
        _repository = repository;
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

        var currentUserId = _identityProvider.Current.User.UserId;

        // Check if user already has a testimonial (one per user). A second one
        // is a conflict of state, which is what the controller declares and what
        // the form on the other side branches on: Gone said neither "not found"
        // nor "deleted" here and matched nothing the caller was written for.
        var existing = await _repository.GetByAuthor(currentUserId);
        if (existing != null)
        {
            throw new HttpException(HttpStatusCode.Conflict, "У вас уже есть отзыв");
        }

        var entity = new CreateWebsiteTestimonialEntity
        {
            Id = _guidFactory.Create(),
            AuthorId = currentUserId,
            CreatedUtc = _dateTimeProvider.Now.UtcDateTime,
            Text = createTestimonial.Text
        };

        var result = await _repository.Create(entity);
        await _producer.SendAsync(EventType.NewWebsiteTestimonial, result.Id);

        return result;
    }

    /// <inheritdoc />
    public async Task<WebsiteTestimonial> GetAsync(Guid id)
    {
        var testimonial = await _repository.Get(id);
        if (testimonial == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Отзыв не найден");
        }

        return testimonial;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<WebsiteTestimonial> Testimonials, PagingResult Paging)> GetListAsync(
        WebsiteTestimonialsQuery query)
    {
        var totalCount = await _repository.Count(query);
        var pageSize = _identityProvider.Current.Settings.Paging.EntitiesPerPage;
        var pagingData = new PagingData(query, pageSize, (int)totalCount);
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
