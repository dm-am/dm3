using System;
using System.Linq;
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
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaTemplateRepository _repository;
    private readonly AttributeSchemaTemplateService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public AttributeSchemaTemplateServiceShould()
    {
        _intentionManager = Mock<IIntentionManager>();
        _repository = Mock<IAttributeSchemaTemplateRepository>();

        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(_userId, UserRole.SeniorModerator));

        _service = new AttributeSchemaTemplateService(
            _intentionManager,
            _repository,
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
        _repository.GetTemplates(Arg.Any<CancellationToken>()).Returns([]);

        await _service.GetAllAsync();

        _intentionManager.DidNotReceive().ThrowIfForbidden(Arg.Any<AttributeSchemaTemplateIntention>());
    }

    [Fact]
    public async Task AnswerNotFoundForATemplateNobodyCanRead()
    {
        _repository.GetTemplate(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AttributeSchemaTemplate?)null);

        var act = async () => await _service.GetAsync(Guid.NewGuid());

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
        _repository.Create(Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = Guid.NewGuid(), Title = "Лайт" });

        await _service.CreateAsync(Creation());

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeReplacement()
    {
        var templateId = Guid.NewGuid();
        _repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = templateId, Title = "Лайт" });

        await _service.ReplaceAsync(Replacement(templateId));

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeRemoval()
    {
        _repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.Hidden);

        await _service.DeleteAsync(Guid.NewGuid());

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    [Fact]
    public async Task AuthorizeTheDefaultAssignment()
    {
        _repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        await _service.MakeDefaultAsync(Guid.NewGuid());

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
    }

    /// <summary>
    /// The rank is asked before the body is, so a caller who may not write
    /// learns nothing from the shape of what they sent.
    /// </summary>
    [Fact]
    public async Task RefuseARankBeforeReadingTheBody()
    {
        _intentionManager
            .When(manager => manager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage))
            .Throw(new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied));

        var act = async () => await _service.CreateAsync(new CreateAttributeSchemaTemplate { Title = "" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Create(
            Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The row records who put the template into the catalogue.
    /// </summary>
    [Fact]
    public async Task CreateOnBehalfOfTheCaller()
    {
        var creation = Creation();
        _repository.Create(creation, _userId, Arg.Any<CancellationToken>())
            .Returns(new AttributeSchemaTemplate { Id = Guid.NewGuid(), Title = "Лайт" });

        await _service.CreateAsync(creation);

        await _repository.Received(1).Create(creation, _userId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The title taken by a live template is a conflict, on both writes that
    /// can take one.
    /// </summary>
    [Fact]
    public async Task AnswerConflictWhenACreatedTitleIsTaken()
    {
        _repository.Create(Arg.Any<CreateAttributeSchemaTemplate>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate attribute schema template title"));

        var act = async () => await _service.CreateAsync(Creation());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerConflictWhenAReplacedTitleIsTaken()
    {
        _repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Duplicate attribute schema template title"));

        var act = async () => await _service.ReplaceAsync(Replacement(Guid.NewGuid()));

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenAReplacementFindsNoLiveTemplate()
    {
        _repository.Replace(Arg.Any<ReplaceAttributeSchemaTemplate>(), Arg.Any<CancellationToken>())
            .Returns((AttributeSchemaTemplate?)null);

        var act = async () => await _service.ReplaceAsync(Replacement(Guid.NewGuid()));

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
        _repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.RefusedAsDefault);

        var act = async () => await _service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenARemovalFindsNoLiveTemplate()
    {
        _repository.Delete(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AttributeSchemaTemplateRemoval.NotFound);

        var act = async () => await _service.DeleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The loser of two simultaneous assignments is told to look again.
    /// </summary>
    [Fact]
    public async Task AnswerConflictWhenTheDefaultWasJustAssignedElsewhere()
    {
        _repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateEntityException("Concurrent default attribute schema template"));

        var act = async () => await _service.MakeDefaultAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AnswerNotFoundWhenTheAssignmentFindsNoLiveTemplate()
    {
        _repository.MakeDefault(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _service.MakeDefaultAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    /// <summary>
    /// No write here reaches a game. A game keeps a copy of its own sheet, so
    /// nothing about the catalogue can move one - and the way to keep that true
    /// is to hand this service nothing that could.
    /// </summary>
    [Fact]
    public void BeHandedNothingThatCanReachAGame() =>
        typeof(AttributeSchemaTemplateService)
            .GetConstructors().Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .Where(name => name.Contains("Game", StringComparison.Ordinal))
            .Should().BeEmpty("a template that could reach a game would eventually change one");
}
