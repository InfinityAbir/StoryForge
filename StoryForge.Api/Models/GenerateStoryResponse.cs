namespace StoryForge.Api.Models;

public class GenerateStoryResponse
{
    public string Title { get; set; } = string.Empty;
    public string Story { get; set; } = string.Empty;
    public StoryDna StoryDna { get; set; } = new();
}

public class ApiErrorResponse
{
    public ApiError Error { get; set; } = new();
}

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
