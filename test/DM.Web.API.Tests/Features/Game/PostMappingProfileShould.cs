using System;
using System.Linq;
using AutoMapper;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Game.AttributeSchemas;
using DM.Web.API.Features.Game.Characters;
using DM.Web.API.Features.Game.Games;
using DM.Web.API.Features.Game.Posts;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using FluentAssertions;
using Xunit;
using DomainCreatePost = DM.Domain.Game.Features.Posts.CreatePost;
using DomainUpdatePost = DM.Domain.Game.Features.Posts.UpdatePost;

namespace DM.Web.API.Tests.Features.Game;

public class PostMappingProfileShould : UnitTestBase
{
    private readonly MapperConfiguration _configuration = new(cfg =>
    {
        cfg.AddProfile<UserMappingProfile>();
        cfg.AddProfile<UserRefMappingProfile>();
        cfg.AddProfile<CharacterMappingProfile>();
        cfg.AddProfile<AttributeSchemaMappingProfile>();
        cfg.AddProfile<GameMappingProfile>();
        cfg.AddProfile<BbTextMappingProfile>();
        cfg.AddProfile<PostMappingProfile>();
    });

    private readonly IMapper _mapper;

    public PostMappingProfileShould() => _mapper = _configuration.CreateMapper();

    /// <summary>
    /// Editing a post cannot touch its character.
    /// </summary>
    /// <remarks>
    /// The PATCH path used to map the read model onto the update model, where
    /// CharacterId is an Optional&lt;Guid&gt; — a wrapper with a private
    /// constructor and no converter registered anywhere, which the service reads
    /// as an instruction to detach the character when it arrives carrying no
    /// value. The request DTO has no character field at all, so the hazard is
    /// gone by construction rather than by a conversion that happens to work;
    /// this pins that it stays gone.
    /// </remarks>
    [Fact]
    public void MapAPostEditWithoutTouchingTheCharacter()
    {
        var request = new UpdatePostRequest { GameText = "text", MetagameText = "ooc" };

        var update = _mapper.Map<DomainUpdatePost>(request);

        update.GameText.Should().Be("text");
        update.MetagameText.Should().Be("ooc");
        update.CharacterId.Should().BeNull(
            "an absent character is what leaves the stored one alone");
    }

    /// <summary>
    /// The response DTO is not a request body, and the mapper has no route for it.
    /// </summary>
    [Fact]
    public void NotMapTheResponseDtoOntoTheUpdateModel()
    {
        var post = new Post { Character = new Character { Id = Guid.NewGuid() } };

        var map = () => _mapper.Map<DomainUpdatePost>(post);

        map.Should().Throw<AutoMapperMappingException>(
            "a body typed as the response DTO is the defect RequestBodyTypesShould names");
    }
    [Fact]
    public void MapCreateDiceRollRequestToDomainSpec()
    {
        var request = new CreatePostRequest
        {
            GameText = "text",
            DiceRolls = new[]
            {
                new CreatePostDiceRoll
                {
                    Dice = 20, Count = 2, Bonus = 3, Explosion = 1, Public = false, Comment = "hit"
                }
            }
        };

        var domain = _mapper.Map<DomainCreatePost>(request);

        var spec = domain.DiceRolls.Single();
        spec.EdgesCount.Should().Be(20);   // Dice → EdgesCount
        spec.DiceCount.Should().Be(2);     // Count → DiceCount
        spec.Bonus.Should().Be(3);
        spec.ExplosionCount.Should().Be(1); // Explosion → ExplosionCount
        spec.IsHidden.Should().BeTrue();   // !Public → IsHidden
        spec.Comment.Should().Be("hit");
    }
}
