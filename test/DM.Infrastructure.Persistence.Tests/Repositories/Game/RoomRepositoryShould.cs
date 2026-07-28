using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Repositories.Game;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

public class RoomRepositoryShould : UnitTestBase, IDisposable
{
    private readonly DmDbContext _dbContext;
    private readonly RoomRepository _repository;
    private readonly Guid _masterId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _roomId = Guid.NewGuid();

    public RoomRepositoryShould()
    {
        _dbContext = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var mapper = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<GameMappingProfile>();
            cfg.AddProfile<GeneralUserMappingProfile>();
        }).CreateMapper();

        _repository = new RoomRepository(_dbContext, mapper);
    }

    private async Task SeedRoomAsync(bool isArchived)
    {
        _dbContext.Games.Add(new DbGame
        {
            GameId = _gameId,
            PublicId = "abcde",
            Title = "Test Game",
            MasterId = _masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved
        });
        _dbContext.Rooms.Add(new DbRoom
        {
            RoomId = _roomId,
            GameId = _gameId,
            RoomNumber = 1,
            Title = "Test Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
            IsArchived = isArchived
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PersistIsArchivedWhenUpdateSetsIt()
    {
        await SeedRoomAsync(isArchived: false);

        var result = await _repository.Update(new UpdateRoomEntity
        {
            RoomId = _roomId,
            IsArchived = true
        });

        result.IsArchived.Should().BeTrue();
        var stored = await _dbContext.Rooms.FindAsync(_roomId);
        stored!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task KeepStoredIsArchivedWhenUpdateOmitsIt()
    {
        await SeedRoomAsync(isArchived: true);

        var result = await _repository.Update(new UpdateRoomEntity
        {
            RoomId = _roomId,
            Title = "Renamed Room",
            IsArchived = null
        });

        result.IsArchived.Should().BeTrue();
        result.Title.Should().Be("Renamed Room");
        var stored = await _dbContext.Rooms.FindAsync(_roomId);
        stored!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task KeepArchivedRoomRetrievable()
    {
        await SeedRoomAsync(isArchived: true);

        var room = await _repository.GetAvailable(_roomId, _masterId);

        room.Should().NotBeNull();
        room!.IsArchived.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
