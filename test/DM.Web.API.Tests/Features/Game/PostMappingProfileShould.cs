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

    [Fact]
    public void HaveValidConfiguration() => _configuration.AssertConfigurationIsValid();

    /// <summary>
    /// The PATCH path maps the read model onto the update model, where CharacterId
    /// is an Optional&lt;Guid&gt; — a wrapper with a private constructor and no
    /// converter registered anywhere. AssertConfigurationIsValid does not exercise
    /// the conversion, so this pins what actually happens at runtime.
    /// </summary>
    [Fact]
    public void MapPostToUpdatePostWithoutCorruptingTheCharacter()
    {
        var characterId = Guid.NewGuid();
        var post = new Post
        {
            Character = new Character { Id = characterId },
        };

        var update = _mapper.Map<DomainUpdatePost>(post);

        // Either the wrapper carries the id, or the field stays absent — both are
        // survivable. What must not happen is a wrapper carrying no value: the
        // service reads that as an instruction to detach the character.
        if (update.CharacterId != null)
        {
            update.CharacterId.Value.Should().Be(characterId);
        }
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
