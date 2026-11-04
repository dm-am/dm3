using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Characters;

/// <summary>
/// Who sees a character. An application under review is seen by everybody, a
/// guest included; a declined one by nobody, the applicant and the master,
/// assistants and mentor included, on the list and by a direct link alike.
/// </summary>
public class CharacterVisibilityShould : CharacterServiceTestBase
{
    private readonly Guid gameId = Guid.NewGuid();
    private readonly Guid masterId = Guid.NewGuid();
    private readonly Guid assistantId = Guid.NewGuid();
    private readonly Guid mentorId = Guid.NewGuid();
    private readonly Guid authorId = Guid.NewGuid();

    public CharacterVisibilityShould()
    {
        gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = masterId, Username = "Master" },
            Assistants = [new GameAssistantInfo { UserId = assistantId, Username = "Assistant" }],
            Mentor = new GeneralUser { UserId = mentorId, Username = "Mentor" }
        });
    }

    public enum Viewer
    {
        Guest,
        Stranger,
        Author,
        Master,
        Assistant,
        Mentor
    }

    public static TheoryData<Viewer> EveryViewer => new(Enum.GetValues<Viewer>());

    /// <summary>
    /// The refusal is the one an unknown identifier gets, word for word, so the
    /// answer does not tell a declined application from a character that never was.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryViewer))]
    public async Task AnswerADeclinedApplicationAsACharacterThatIsNotThere(Viewer viewer)
    {
        var characterId = StoredCharacter(CharacterStatus.Declined);
        View(viewer);

        var act = () => service.GetAsync(characterId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.CharacterNotFound);
    }

    [Theory]
    [InlineData(CharacterStatus.UnderReview, Viewer.Guest)]
    [InlineData(CharacterStatus.UnderReview, Viewer.Stranger)]
    [InlineData(CharacterStatus.UnderReview, Viewer.Author)]
    [InlineData(CharacterStatus.UnderReview, Viewer.Master)]
    [InlineData(CharacterStatus.Active, Viewer.Guest)]
    [InlineData(CharacterStatus.Active, Viewer.Stranger)]
    [InlineData(CharacterStatus.Retired, Viewer.Guest)]
    [InlineData(CharacterStatus.Retired, Viewer.Stranger)]
    public async Task ShowAnyCharacterButADeclinedOneToAnybody(CharacterStatus status, Viewer viewer)
    {
        var characterId = StoredCharacter(status);
        View(viewer);

        var character = await service.GetAsync(characterId);

        character.Id.Should().Be(characterId);
    }

    [Theory]
    [MemberData(nameof(EveryViewer))]
    public async Task ListEveryCharacterButTheDeclinedOnes(Viewer viewer)
    {
        ListedCharacters();
        View(viewer);

        var characters = await service.GetAllAsync(gameId);

        characters.Select(c => c.Status).Should().BeEquivalentTo(
            [CharacterStatus.Active, CharacterStatus.Retired, CharacterStatus.UnderReview]);
    }

    private Guid StoredCharacter(CharacterStatus status)
    {
        var character = CharacterOfTheAuthor(status);
        repository.FindCharacter(character.Id).Returns(character);
        return character.Id;
    }

    private void ListedCharacters()
    {
        repository.GetCharacters(gameId).Returns(new[]
        {
            CharacterOfTheAuthor(CharacterStatus.Active),
            CharacterOfTheAuthor(CharacterStatus.Retired),
            CharacterOfTheAuthor(CharacterStatus.UnderReview),
            CharacterOfTheAuthor(CharacterStatus.Declined)
        });
    }

    private Character CharacterOfTheAuthor(CharacterStatus status) => new()
    {
        Id = Guid.NewGuid(),
        GameId = gameId,
        Status = status,
        Author = new GeneralUser { UserId = authorId, Username = "Author" }
    };

    private void View(Viewer viewer)
    {
        identityProvider.Current.Returns(viewer switch
        {
            Viewer.Guest => Identities.Guest(),
            Viewer.Stranger => Identities.User(Guid.NewGuid()),
            Viewer.Author => Identities.User(authorId),
            Viewer.Master => Identities.User(masterId),
            Viewer.Assistant => Identities.User(assistantId),
            Viewer.Mentor => Identities.User(mentorId),
            _ => throw new ArgumentOutOfRangeException(nameof(viewer))
        });
    }
}
