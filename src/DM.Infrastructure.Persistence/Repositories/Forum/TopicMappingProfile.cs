using AutoMapper;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Entities.Shared;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Profile for topic DTO and DAL mapping.
///
/// Performance note (PERFORMANCE.md: "Avoid inline aggregations"):
/// <see cref="Topic.TotalCommentsCount"/> is intentionally left
/// unmapped (Ignore) here. Mapping it via
/// <c>t.Comments.Count()</c> inside <c>ProjectTo</c> would generate one
/// correlated <c>SELECT COUNT(*)</c> subquery per row in SQL. The
/// repository instead fills it in a single batched <c>GROUP BY</c> pass
/// after the main projection.
/// </summary>
internal class TopicMappingProfile : Profile
{
    /// <inheritdoc />
    public TopicMappingProfile()
    {
        CreateMap<Comment, LastComment>()
            .ForMember(d => d.Id, opt => opt.Ignore());

        CreateMap<TopicEntity, Topic>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.TopicId))
            .ForMember(d => d.TopicNumber, s => s.MapFrom(t => t.TopicNumber))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(t => t.LastComment == null
                ? t.CreatedUtc
                : t.LastComment.CreatedUtc))
            // Filled by the repository post-projection via a single
            // batched GROUP BY. See comment above.
            .ForMember(d => d.TotalCommentsCount, opt => opt.Ignore())
            .ForMember(d => d.UnreadCommentsCount, opt => opt.Ignore())
            .ForMember(d => d.ModifiedUtc, opt => opt.Ignore())
            .ForMember(d => d.Likes, s => s.Ignore())
            // Same rationale as TotalCommentsCount: avoid a per-row
            // correlated SELECT COUNT(*) on Likes; fill in a single
            // batched GROUP BY after the main projection.
            .ForMember(d => d.LikesCount, opt => opt.Ignore());
    }
}
