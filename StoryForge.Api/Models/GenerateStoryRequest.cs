namespace StoryForge.Api.Models;

public class GenerateStoryRequest
{
    public string Mode { get; set; } = string.Empty; // initial | regenerate | feedback
    public string? Story { get; set; }
    public StoryDna? StoryDna { get; set; }
    public string? PreviousStory { get; set; }
    public string? Feedback { get; set; }
    public string InputLanguage { get; set; } = "auto"; // auto | english | bangla
    public string OutputLanguage { get; set; } = "same"; // same | english | bangla
    public string Length { get; set; } = "medium"; // short | medium | long
}
