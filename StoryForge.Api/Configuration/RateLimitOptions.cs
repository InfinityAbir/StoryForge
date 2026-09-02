namespace StoryForge.Api.Configuration;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
    public int HourlyPermitLimit { get; set; } = 60;
    public int HourlyWindowSeconds { get; set; } = 3600;
    public int QueueLimit { get; set; } = 0;
}
