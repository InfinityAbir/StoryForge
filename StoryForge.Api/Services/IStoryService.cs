using StoryForge.Api.Models;

namespace StoryForge.Api.Services;

public interface IStoryService
{
    Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken);
}
