using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.Posts;

public class PostEdit
{
    public Guid Id { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public GeneralUser Editor { get; set; } = null!;
}
