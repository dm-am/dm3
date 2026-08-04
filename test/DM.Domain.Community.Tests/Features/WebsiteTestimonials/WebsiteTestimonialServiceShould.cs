using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Community.Authorization;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Community.Tests.Features.WebsiteTestimonials;

public class WebsiteTestimonialServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<CreateWebsiteTestimonial>> _createValidator;
    private readonly Mock<IValidator<UpdateWebsiteTestimonial>> _updateValidator;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IWebsiteTestimonialRepository> _repository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly WebsiteTestimonialService _service;
    private readonly Guid _currentUserId;

    public WebsiteTestimonialServiceShould()
    {
        _createValidator = Mock<IValidator<CreateWebsiteTestimonial>>();
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateWebsiteTestimonial>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator = Mock<IValidator<UpdateWebsiteTestimonial>>();
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateWebsiteTestimonial>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IWebsiteTestimonialRepository>();

        _eventProducer = Mock<IEventProducer>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new WebsiteTestimonialService(
            _createValidator.Object,
            _updateValidator.Object,
            _intentionManager.Object,
            _repository.Object,
            _eventProducer.Object,
            _dateTimeProvider.Object,
            _guidFactory.Object,
            _identityProvider.Object);
    }

    #region Create Tests

    [Fact]
    public async Task AuthorizeCreateAction()
    {
        var expectedTestimonial = new WebsiteTestimonial { Id = Guid.NewGuid(), Text = "Great website!" };
        _repository.Setup(r => r.GetByAuthor(_currentUserId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        await _service.CreateAsync(new CreateWebsiteTestimonial { Text = "Great website!" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Create), Times.Once);
    }

    [Fact]
    public async Task ThrowGoneWhenUserAlreadyHasTestimonial()
    {
        var existingTestimonial = new WebsiteTestimonial
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Existing testimonial"
        };
        _repository.Setup(r => r.GetByAuthor(_currentUserId)).ReturnsAsync(existingTestimonial);

        var act = async () => await _service.CreateAsync(new CreateWebsiteTestimonial { Text = "New testimonial!" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Gone);
    }

    [Fact]
    public async Task CreateTestimonialSuccessfully()
    {
        var expectedTestimonial = new WebsiteTestimonial
        {
            Id = Guid.NewGuid(),
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Great website!"
        };
        _repository.Setup(r => r.GetByAuthor(_currentUserId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        var result = await _service.CreateAsync(new CreateWebsiteTestimonial { Text = "Great website!" });

        result.Should().Be(expectedTestimonial);
        _repository.Verify(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>()), Times.Once);
    }

    [Fact]
    public async Task SendEventOnCreate()
    {
        var testimonialId = Guid.NewGuid();
        var expectedTestimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.Setup(r => r.GetByAuthor(_currentUserId)).ReturnsAsync((WebsiteTestimonial?)null);
        _repository.Setup(r => r.Create(It.IsAny<CreateWebsiteTestimonialEntity>())).ReturnsAsync(expectedTestimonial);

        await _service.CreateAsync(new CreateWebsiteTestimonial { Text = "Great website!" });

        _eventProducer.Verify(e => e.SendAsync(EventType.NewWebsiteTestimonial, testimonialId), Times.Once);
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task ThrowNotFoundWhenTestimonialDoesNotExist()
    {
        var testimonialId = Guid.NewGuid();
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync((WebsiteTestimonial?)null);

        var act = async () => await _service.GetAsync(testimonialId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTestimonialWhenExists()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial { Id = testimonialId, Text = "Great website!" };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

        var result = await _service.GetAsync(testimonialId);

        result.Should().Be(testimonial);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task AuthorizeEditAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text"
        };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);
        _repository.Setup(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>())).ReturnsAsync(testimonial);

        await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Edit, testimonial), Times.Once);
    }

    [Fact]
    public async Task UpdateTestimonialSuccessfully()
    {
        var testimonialId = Guid.NewGuid();
        var existingTestimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Original text"
        };
        var updatedTestimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Updated text"
        };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(existingTestimonial);
        _repository.Setup(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>())).ReturnsAsync(updatedTestimonial);

        var result = await _service.UpdateAsync(new UpdateWebsiteTestimonial { Id = testimonialId, Text = "Updated text" });

        result.Should().Be(updatedTestimonial);
        _repository.Verify(r => r.Update(It.IsAny<UpdateWebsiteTestimonialEntity>()), Times.Once);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task AuthorizeDeleteAction()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

        await _service.DeleteAsync(testimonialId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(WebsiteTestimonialIntention.Delete, testimonial), Times.Once);
    }

    [Fact]
    public async Task DeleteTestimonialSuccessfully()
    {
        var testimonialId = Guid.NewGuid();
        var testimonial = new WebsiteTestimonial
        {
            Id = testimonialId,
            Author = new GeneralUser { UserId = _currentUserId },
            Text = "Some text"
        };
        _repository.Setup(r => r.Get(testimonialId)).ReturnsAsync(testimonial);

        await _service.DeleteAsync(testimonialId);

        _repository.Verify(r => r.Delete(testimonialId, _currentUserId), Times.Once);
    }

    #endregion
}
