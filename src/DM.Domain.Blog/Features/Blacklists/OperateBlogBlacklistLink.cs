using System;

namespace DM.Domain.Blog.Features.Blacklists;

public class OperateBlogBlacklistLink
{
    public Guid BlogId { get; set; }

    public string Username { get; set; } = null!;
}
