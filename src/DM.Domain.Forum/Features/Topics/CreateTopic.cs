namespace DM.Domain.Forum.Features.Topics;

public class CreateTopic
{
    public string BoardTitle { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Text { get; set; } = null!;
}
