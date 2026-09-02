namespace StoryForge.Api.Configuration;

public class StoryOptions
{
    public const string SectionName = "Story";

    public int MinCharacters { get; set; } = 100;
    public int MaxCharacters { get; set; } = 60000;
    public int MaxFeedbackCharacters { get; set; } = 5000;
}
