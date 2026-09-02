namespace StoryForge.Api.Configuration;

public class GroqOptions
{
    public const string SectionName = "Groq";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "openai/gpt-oss-120b";
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1/";
    public int TimeoutSeconds { get; set; } = 60;
}
