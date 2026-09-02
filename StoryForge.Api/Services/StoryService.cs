using Microsoft.Extensions.Options;
using StoryForge.Api.Configuration;
using StoryForge.Api.Models;

namespace StoryForge.Api.Services;

public class StoryService : IStoryService
{
    private static readonly HashSet<string> ValidModes = ["initial", "regenerate", "feedback"];
    private static readonly HashSet<string> ValidLengths = ["short", "medium", "long"];
    private static readonly HashSet<string> ValidInputLanguages = ["auto", "english", "bangla"];
    private static readonly HashSet<string> ValidOutputLanguages = ["same", "english", "bangla"];

    private readonly IGroqService _groqService;
    private readonly StoryOptions _storyOptions;

    public StoryService(IGroqService groqService, IOptions<StoryOptions> storyOptions)
    {
        _groqService = groqService;
        _storyOptions = storyOptions.Value;
    }

    public Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        return _groqService.GenerateAsync(request, cancellationToken);
    }

    private void Validate(GenerateStoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mode) || !ValidModes.Contains(request.Mode))
        {
            throw StoryForgeException.Validation("Invalid or missing mode.");
        }

        if (!ValidInputLanguages.Contains(request.InputLanguage))
        {
            throw StoryForgeException.Validation("Invalid inputLanguage.");
        }

        if (!ValidOutputLanguages.Contains(request.OutputLanguage))
        {
            throw StoryForgeException.Validation("Invalid outputLanguage.");
        }

        if (!ValidLengths.Contains(request.Length))
        {
            throw StoryForgeException.Validation("Invalid length.");
        }

        switch (request.Mode)
        {
            case "initial":
                ValidateStory(request.Story);
                break;

            case "regenerate":
                ValidateStoryDna(request.StoryDna);
                break;

            case "feedback":
                ValidateStoryDna(request.StoryDna);
                if (string.IsNullOrWhiteSpace(request.PreviousStory))
                {
                    throw StoryForgeException.Validation("previousStory is required for feedback mode.");
                }
                if (request.PreviousStory.Length > 80000)
                {
                    throw StoryForgeException.Validation("previousStory is too long.");
                }
                ValidateFeedback(request.Feedback);
                break;
        }
    }

    private void ValidateStory(string? story)
    {
        if (string.IsNullOrWhiteSpace(story))
        {
            throw StoryForgeException.Validation("Please enter a story first.");
        }

        if (story.Length < _storyOptions.MinCharacters)
        {
            throw StoryForgeException.Validation("Please provide a little more story so StoryForge can understand its narrative DNA.");
        }

        if (story.Length > _storyOptions.MaxCharacters)
        {
            throw StoryForgeException.Validation("This story is too long for the current limit. Please shorten it.");
        }
    }

    private void ValidateFeedback(string? feedback)
    {
        if (string.IsNullOrWhiteSpace(feedback))
        {
            throw StoryForgeException.Validation("Please tell StoryForge what you'd like to change.");
        }

        if (feedback.Length > _storyOptions.MaxFeedbackCharacters)
        {
            throw StoryForgeException.Validation("Feedback is too long. Please shorten it.");
        }
    }

    private static void ValidateStoryDna(StoryDna? dna)
    {
        if (dna is null)
        {
            throw StoryForgeException.Validation("storyDna is required for this mode.");
        }

        if (dna.Genre.Count == 0 || dna.Themes.Count == 0 || dna.Tone.Count == 0 ||
            dna.EmotionalArc.Count == 0 || dna.Structure.Count == 0 ||
            string.IsNullOrWhiteSpace(dna.ProtagonistArchetype) ||
            string.IsNullOrWhiteSpace(dna.CentralConflict) ||
            string.IsNullOrWhiteSpace(dna.EndingType))
        {
            throw StoryForgeException.Validation("storyDna is incomplete.");
        }

        if (dna.Genre.Count > 5 || dna.Themes.Count > 8 || dna.Tone.Count > 6 ||
            dna.EmotionalArc.Count > 10 || dna.Structure.Count > 10 ||
            dna.ProtagonistArchetype.Length > 200 || dna.CentralConflict.Length > 300 ||
            dna.EndingType.Length > 200)
        {
            throw StoryForgeException.Validation("storyDna exceeds allowed size.");
        }
    }
}
