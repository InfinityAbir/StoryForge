namespace StoryForge.Api.Models;

public class StoryDna
{
    public List<string> Genre { get; set; } = [];
    public List<string> Themes { get; set; } = [];
    public List<string> Tone { get; set; } = [];
    public string ProtagonistArchetype { get; set; } = string.Empty;
    public string CentralConflict { get; set; } = string.Empty;
    public List<string> EmotionalArc { get; set; } = [];
    public List<string> Structure { get; set; } = [];
    public string EndingType { get; set; } = string.Empty;
}
