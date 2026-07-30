using System;
using System.Linq;
using AutoMapper;
using DM.Domain.Core.Comments;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Profile for comment mapping
/// </summary>
/// <remarks>
/// The only map for this pair. Forum, game, blog and publication comments are the
/// same entity and the same DTO, so a second declaration in a module profile
/// specialises nothing — it shadows this one, and which of the two the application
/// ran on depended on the order reflection happened to hand the profiles to the
/// container.
/// </remarks>
internal class CommentMappingProfile : Profile
{
    /// <inheritdoc />
    public CommentMappingProfile()
    {
        CreateMap<Entities.Shared.Comment, Comment>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.CommentId))
            // The edit history is the modification time: the entity keeps every
            // edit instead of overwriting one column, so "edited at" is the most
            // recent of them.
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(c => c.Edits
                .OrderByDescending(e => e.EditedUtc)
                .Select(e => (DateTimeOffset?)e.EditedUtc)
                .FirstOrDefault()))
            .ForMember(d => d.Likes, s => s.Ignore()); // Likes fetched via EntityType+EntityId pattern
    }
}
