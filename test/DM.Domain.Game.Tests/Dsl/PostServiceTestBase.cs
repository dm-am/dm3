using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.RoomAccesses;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Game.Tests.Dsl;

/// <summary>
/// The post service over substitutes of everything it asks: validators that
/// pass, intentions that allow, a signed-in user, a producer that takes every
/// event and tag names written as they came.
/// </summary>
/// <remarks>
/// The room and post repositories answer nothing until a case tells them what
/// to: the room a post goes to is the case's own, built with the helpers below.
/// </remarks>
public abstract class PostServiceTestBase : UnitTestBase
{
    protected readonly IIntentionManager intentionManager;
    protected readonly IRoomService roomService;
    protected readonly IRoomRepository roomRepository;
    protected readonly IPostRepository repository;
    protected readonly IDiceRollRepository diceRollRepository;
    protected readonly IDiceRoller diceRoller;
    protected readonly IUnreadCountersRepository unreadCountersRepository;
    protected readonly IEventProducer producer;
    protected readonly IIdentityProvider identityProvider;
    protected readonly Guid currentUserId = Guid.NewGuid();

    /// <summary>
    /// The id the factory hands the service. The post id is generated before the
    /// write rather than read back from it — that is what lets the dice be stored
    /// first — so a test that wants to talk about the post has to know it up front.
    /// </summary>
    protected readonly Guid postId = Guid.NewGuid();
    private protected readonly PostService service;

    protected PostServiceTestBase()
    {
        var createValidator = Mock<IValidator<CreatePost>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreatePost>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdatePost>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdatePost>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        roomService = Mock<IRoomService>();

        roomRepository = Mock<IRoomRepository>();

        intentionManager = Mock<IIntentionManager>();
        intentionManager.IsAllowed(Arg.Any<PostIntention>(), Arg.Any<object>()).Returns(true);
        intentionManager.IsAllowed(Arg.Any<RoomIntention>(), Arg.Any<object>()).Returns(true);

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(postId);

        repository = Mock<IPostRepository>();

        diceRollRepository = Mock<IDiceRollRepository>();
        diceRoller = Mock<IDiceRoller>();

        unreadCountersRepository = Mock<IUnreadCountersRepository>();

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);
        producer.SendAsync(Arg.Any<IEnumerable<EventType>>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        identityProvider = Mock<IIdentityProvider>();
        var identity = Identities.User(currentUserId, "testuser");
        identityProvider.Current.Returns(identity);

        // The text as it came, unless a test says otherwise.

        service = new PostService(
            createValidator,
            updateValidator,
            roomService,
            roomRepository,
            intentionManager,
            dateTimeProvider,
            guidFactory,
            repository,
            diceRollRepository,
            diceRoller,
            unreadCountersRepository,
            producer,
            identityProvider,
            new PrivateBlockReading(new BbParserProvider()));
    }

    /// <summary>A room whose access list holds the given characters.</summary>
    protected static RoomToUpdate RoomWith(Guid roomId, params (string Name, Guid OwnerId)[] characters) =>
        RoomWithCharacters(roomId, characters
            .Select(c => (Guid.NewGuid(), c.Name, c.OwnerId))
            .ToArray());

    /// <summary>The same, when the test has to say which character is which.</summary>
    protected static RoomToUpdate RoomWithCharacters(
        Guid roomId, params (Guid CharacterId, string Name, Guid OwnerId)[] characters)
    {
        var accesses = characters.Select(c => new RoomAccess
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            TargetType = RoomAccessTargetType.Character,
            Character = new Character
            {
                Id = c.CharacterId,
                Name = c.Name,
                Author = new GeneralUser { UserId = c.OwnerId }
            }
        }).ToList();

        return new RoomToUpdate
        {
            Id = roomId,
            Accesses = accesses,
            Game = new GameDto()
        };
    }
}
