using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Messaging.Features.Messages;

public class MessageEdit
{
    public Guid Id { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public GeneralUser Editor { get; set; } = null!;
}
