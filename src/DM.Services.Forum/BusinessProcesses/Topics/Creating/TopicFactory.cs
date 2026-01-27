using System;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.Forum.Dto.Input;

namespace DM.Services.Forum.BusinessProcesses.Topics.Creating;

/// <inheritdoc />
internal class TopicFactory : ITopicFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public TopicFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public ForumTopic Create(Guid boardId, Guid userId, CreateTopic createTopic)
    {
        return new ForumTopic
        {
            BoardId = boardId,
            ForumTopicId = _guidFactory.Create(),
            CreatedUtc = _dateTimeProvider.Now,
            UserId = userId,
            Title = createTopic.Title.Trim(),
            Text = createTopic.Text?.Trim()
        };
    }
}