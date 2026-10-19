using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

public class BlogUser
{
    public GeneralUser User { get; set; } = null!;

    public BlogRole Role { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
