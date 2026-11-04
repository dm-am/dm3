using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemaTemplates;

/// <summary>
/// The catalogue of starting character sheets: who may change it, and what each
/// refusal of the storage means to the caller.
/// </summary>
public class AttributeSchemaTemplateServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IAttributeSchemaTemplateRepository repository;
    private readonly AttributeSchemaTemplateService service;
    private readonly Guid userId = Guid.NewGuid();

    public AttributeSchemaTemplateServiceShould()
    {
        intentionManager = Mock<IIntentionManager>();
        repository = Mock<IAttributeSchemaTemplateRepository>();

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(userId, UserRole.SeniorModerator));

        service = new AttributeSchemaTemplateService(
            intentionManager,
            repository,
            identityProvider,
            new CreateAttributeSchemaTemplateValidator(),
            new ReplaceAttributeSchemaTemplateValidator());
    }

    private static CreateAttributeSchemaTemplate Creation(string title = "Лайт") => new() { Title = title };

    private static ReplaceAttributeSchemaTemplate Replacement(Guid templateId, string title = "Лайт") =>
        new() { TemplateId = templateId, Title = title };

    /// <summary>
    /// Reading the catalogue asks nobody anything.
    /// </summary>
    [Fact]
    public async Task ReadTheCatalogueWithoutAGate()
    {
        repository.GetTemplates(Arg.Any<CancellationToken>()).Returns([]);

        await service.GetAllAsync();

        intentionManager.DidNotReceive().ThrowIfForbidden(Arg.Any<AttributeSchemaTemplateIntention>());
    }

    [Fact]
    public async Task AnswerNotFoundForATemplateNobodyCanRead()
    {
        repository.GetTemplate(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AttributeSchemaTemplate?)null);

        var act = async () => await service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Every write asks the same question, and asks it before it touches the
    /// storage.
    /// </summary>
    [Fact]
    public async Task AuthorizeCreation()
    {
        repository.Create(Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = Guid.NewGuid(), Title = "Лайт" });

        await service.CreateAsync(Creation());

        intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeReplacement()
    {
        var templateId = Guid.NewGuid();
        repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = templateId, Title = "Лайт" });

        await service.ReplaceAsync(Replacement(templateId));

        intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeRemoval()
    {
        repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.Hidden);

        await service.DeleteAsync(Guid.NewGuid());

        intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeTheDefaultAssignment()
    {
        repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        await service.MakeDefaultAsync(Guid.NewGuid());

        intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    /// <summary>
    /// The rank is asked before the body is, so a caller who may not write
    /// learns nothing from the shape of what they sent.
    /// </summary>
    [Fact]
    public async Task RefuseARankBeforeReadingTheBody()
    {
        intentionManager
            .When(manager => manager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage))
            .Throw(new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied));

        var act = async () => await service.CreateAsync(new CreateAttributeSchemaTemplate { Title = "" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Create(
            Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The row records who put the template into the catalogue.
    /// </summary>
    [Fact]
    public async Task CreateOnBehalfOfTheCaller()
    {
        var creation = Creation();
        repository.Create(creation, userId, Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = Guid.NewGuid(), Title = "Лайт" });

        await service.CreateAsync(creation);

        await repository.Received(1).Create(creation, userId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The title taken by a live template is a conflict, on both writes that
    /// can take one.
    /// </summary>
    [Fact]
    public async Task AnswerConflictWhenACreatedTitleIsTaken()
    {
        repository.Create(Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate attribute schema template title"));

        var act = async () => await service.CreateAsync(Creation());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerConflictWhenAReplacedTitleIsTaken()
    {
        repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate attribute schema template title"));

        var act = async () => await service.ReplaceAsync(Replacement(Guid.NewGuid()));

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenAReplacementFindsNoLiveTemplate()
    {
        repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .Returns((AttributeSchemaTemplate?)null);

        var act = async () => await service.ReplaceAsync(Replacement(Guid.NewGuid()));

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The site is never left without a template to start from, so the removal
    /// of the default one is a conflict rather than a repair.
    /// </summary>
    [Fact]
    public async Task RefuseToRemoveTheDefaultTemplate()
    {
        repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.RefusedAsDefault);

        var act = async () => await service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenARemovalFindsNoLiveTemplate()
    {
        repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.NotFound);

        var act = async () => await service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The loser of two simultaneous assignments is told to look again.
    /// </summary>
    [Fact]
    public async Task AnswerConflictWhenTheDefaultWasJustAssignedElsewhere()
    {
        repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Concurrent default attribute schema template"));

        var act = async () => await service.MakeDefaultAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenTheAssignmentFindsNoLiveTemplate()
    {
        repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await service.MakeDefaultAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
