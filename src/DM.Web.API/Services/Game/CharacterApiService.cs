using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Game.BusinessProcesses.Characters.Creating;
using DM.Services.Game.BusinessProcesses.Characters.Deleting;
using DM.Services.Game.BusinessProcesses.Characters.Reading;
using DM.Services.Game.BusinessProcesses.Characters.Updating;
using DM.Services.Game.Dto.Input;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Games;

namespace DM.Web.API.Services.Game;

/// <inheritdoc />
internal class CharacterApiService : ICharacterApiService
{
    private readonly ICharacterReadingService readingService;
    private readonly ICharacterCreatingService creatingService;
    private readonly ICharacterUpdatingService updatingService;
    private readonly ICharacterDeletingService deletingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public CharacterApiService(
        ICharacterReadingService readingService,
        ICharacterCreatingService creatingService,
        ICharacterUpdatingService updatingService,
        ICharacterDeletingService deletingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.creatingService = creatingService;
        this.updatingService = updatingService;
        this.deletingService = deletingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Character>> GetAll(Guid gameId)
    {
        var characters = await readingService.GetCharacters(gameId);
        return new ListEnvelope<Character>(characters.Select(mapper.Map<Character>));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Get(Guid characterId)
    {
        var character = await readingService.GetCharacter(characterId);
        return new Envelope<CharacterDetails>(mapper.Map<CharacterDetails>(character));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Create(Guid gameId, CharacterDetails character)
    {
        var createCharacter = mapper.Map<CreateCharacter>(character);
        createCharacter.GameId = gameId;
        var createdCharacter = await creatingService.Create(createCharacter);
        return new Envelope<CharacterDetails>(mapper.Map<CharacterDetails>(createdCharacter));
    }

    /// <inheritdoc />
    public async Task<Envelope<CharacterDetails>> Update(Guid characterId, CharacterDetails character)
    {
        var updateCharacter = mapper.Map<UpdateCharacter>(character);
        updateCharacter.CharacterId = characterId;
        var updatedCharacter = await updatingService.Update(updateCharacter);
        return new Envelope<CharacterDetails>(mapper.Map<CharacterDetails>(updatedCharacter));
    }

    /// <inheritdoc />
    public Task Delete(Guid characterId) => deletingService.Delete(characterId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid gameId) => readingService.MarkAsRead(gameId);
}
