using System;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Rooms;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using FluentAssertions;
using Xunit;
using DomainRoomAccess = DM.Domain.Game.Features.Games.RoomAccess;

namespace DM.Web.API.Tests.Features.Game;

public class RoomMappingProfileShould : UnitTestBase
{
    private readonly MapperConfiguration _configuration = new(cfg =>
    {
        cfg.AddProfile<UserMappingProfile>();
        cfg.AddProfile<UserRefMappingProfile>();
        cfg.AddProfile<BbTextMappingProfile>();
        cfg.AddProfile<CharacterMappingProfile>();
        cfg.AddProfile<RoomMappingProfile>();
    });

    [Fact]
    public void HaveValidConfiguration() => _configuration.AssertConfigurationIsValid();

    /// <summary>
    /// The policy is what decides who may write in the room. It was mapped with
    /// Ignore(), so every access of every response answered null: the screen that
    /// grants access could not show which grant it had just made, and no client
    /// could tell a spectator from a participant.
    /// </summary>
    [Fact]
    public void CarryTheAccessPolicy()
    {
        var access = _configuration.CreateMapper().Map<RoomAccess>(new DomainRoomAccess
        {
            Id = Guid.NewGuid(),
            TargetType = RoomAccessTargetType.Reader,
            Policy = RoomAccessPolicy.ReadOnly
        });

        access.Policy.Should().Be(RoomAccessPolicy.ReadOnly);
    }
}
