using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;
using CharacterAttribute = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.CharacterAttribute;
using DbCharacter = DM.Services.DataAccess.BusinessObjects.Games.Characters.Character;

namespace DM.Services.Gaming.BusinessProcesses.Characters.Creating;

/// <inheritdoc />
internal class CharacterCreatingRepository : ICharacterCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CharacterCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Character> Create(DbCharacter character, IEnumerable<CharacterAttribute> attributes)
    {
        _dbContext.Characters.Add(character);
        _dbContext.CharacterAttributes.AddRange(attributes);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Characters
            .Where(c => c.CharacterId == character.CharacterId)
            .ProjectTo<Character>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}