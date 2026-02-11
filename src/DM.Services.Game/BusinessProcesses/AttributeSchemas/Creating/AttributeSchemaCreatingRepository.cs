using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto;
using DM.Services.DataAccess;
using DM.Services.DataAccess.MongoIntegration;
using DM.Services.Game.Dto.Shared;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using DbAttributeSchema = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.AttributeSchema;

namespace DM.Services.Game.BusinessProcesses.AttributeSchemas.Creating;

/// <inheritdoc />
internal class AttributeSchemaCreatingRepository : MongoCollectionRepository<DbAttributeSchema>, IAttributeSchemaCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AttributeSchemaCreatingRepository(
        DmMongoClient client,
        DmDbContext dbContext,
        IMapper mapper) : base(client)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<AttributeSchema> Create(DbAttributeSchema schema)
    {
        await Collection.InsertOneAsync(schema);
        var createdSchema = await Collection
            .Find(Filter.Eq(s => s.Id, schema.Id))
            .FirstAsync();

        var result = _mapper.Map<AttributeSchema>(createdSchema);
        if (createdSchema.UserId.HasValue)
        {
            var author = await _dbContext.Users
                .Where(u => u.UserId == createdSchema.UserId.Value)
                .ProjectTo<GeneralUser>(_mapper.ConfigurationProvider)
                .FirstAsync();
            result.Author = author;
        }

        return result;
    }
}