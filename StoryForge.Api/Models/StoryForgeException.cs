namespace StoryForge.Api.Models;

public class StoryForgeException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;

    public static StoryForgeException Validation(string message) =>
        new("VALIDATION_ERROR", message, StatusCodes.Status400BadRequest);

    public static StoryForgeException GenerationFailed() =>
        new("GENERATION_FAILED", "Story generation failed. Please try again.", StatusCodes.Status502BadGateway);

    public static StoryForgeException Timeout() =>
        new("GENERATION_TIMEOUT", "The story took too long to generate. Please try again.", StatusCodes.Status504GatewayTimeout);

    public static StoryForgeException ProviderRefusal() =>
        new("PROVIDER_REFUSAL", "StoryForge couldn't generate this request with the current AI model. Try changing the direction and try again.", StatusCodes.Status422UnprocessableEntity);

    public static StoryForgeException InvalidOutput() =>
        new("INVALID_AI_OUTPUT", "Story generation failed. Please try again.", StatusCodes.Status502BadGateway);
}
