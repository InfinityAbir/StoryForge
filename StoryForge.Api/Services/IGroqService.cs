using StoryForge.Api.Models;

namespace StoryForge.Api.Services;

public interface IGroqService
{
    Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken);
}
