using Microsoft.Extensions.Options;
using Moq;
using StoryForge.Api.Configuration;
using StoryForge.Api.Models;
using StoryForge.Api.Services;
using Xunit;

namespace StoryForge.Api.Tests;

public class StoryServiceTests
{
    private readonly Mock<IGroqService> _groqServiceMock = new();
    private readonly StoryService _sut;

    public StoryServiceTests()
    {
        var storyOptions = Options.Create(new StoryOptions
        {
            MinCharacters = 100,
            MaxCharacters = 60000,
            MaxFeedbackCharacters = 5000
        });

        _groqServiceMock
            .Setup(g => g.GenerateAsync(It.IsAny<GenerateStoryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateStoryResponse
            {
                Title = "Title",
                Story = "Story content",
                StoryDna = ValidDna()
            });

        _sut = new StoryService(_groqServiceMock.Object, storyOptions);
    }

    private static StoryDna ValidDna() => new()
    {
        Genre = ["Fantasy"],
        Themes = ["Greed"],
        Tone = ["Dark"],
        ProtagonistArchetype = "Everyman",
        CentralConflict = "Temptation",
        EmotionalArc = ["Hope", "Loss"],
        Structure = ["Discovery", "Consequence"],
        EndingType = "Bittersweet"
    };

    private static GenerateStoryRequest ValidInitialRequest(string? story = null) => new()
    {
        Mode = "initial",
        Story = story ?? new string('a', 150),
        InputLanguage = "auto",
        OutputLanguage = "same",
        Length = "medium"
    };

    [Fact]
    public async Task Throws_when_mode_missing()
    {
        var request = ValidInitialRequest();
        request.Mode = "";

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
        Assert.Equal("VALIDATION_ERROR", ex.Code);
    }

    [Theory]
    [InlineData("not-a-real-mode")]
    [InlineData("Initial")]
    public async Task Throws_when_mode_not_in_allowlist(string mode)
    {
        var request = ValidInitialRequest();
        request.Mode = mode;

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_story_too_short()
    {
        var request = ValidInitialRequest(new string('a', 50));

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
        Assert.Contains("little more story", ex.Message);
    }

    [Fact]
    public async Task Throws_when_story_too_long()
    {
        var request = ValidInitialRequest(new string('a', 60001));

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
        Assert.Contains("too long", ex.Message);
    }

    [Fact]
    public async Task Throws_when_story_missing_for_initial_mode()
    {
        var request = ValidInitialRequest();
        request.Story = null;

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Succeeds_for_valid_initial_request()
    {
        var request = ValidInitialRequest();

        var result = await _sut.GenerateAsync(request, CancellationToken.None);

        Assert.Equal("Title", result.Title);
        _groqServiceMock.Verify(g => g.GenerateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("weird")]
    [InlineData("")]
    public async Task Throws_when_input_language_invalid(string language)
    {
        var request = ValidInitialRequest();
        request.InputLanguage = language;

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_output_language_invalid()
    {
        var request = ValidInitialRequest();
        request.OutputLanguage = "auto"; // not a valid outputLanguage per spec (only same|english|bangla)

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_length_invalid()
    {
        var request = ValidInitialRequest();
        request.Length = "extra-long";

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_regenerate_missing_story_dna()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "regenerate",
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_regenerate_story_dna_incomplete()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "regenerate",
            StoryDna = new StoryDna(), // empty lists / blank strings
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Succeeds_for_valid_regenerate_request()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "regenerate",
            StoryDna = ValidDna(),
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        var result = await _sut.GenerateAsync(request, CancellationToken.None);

        Assert.Equal("Title", result.Title);
    }

    [Fact]
    public async Task Throws_when_feedback_missing_previous_story()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "feedback",
            StoryDna = ValidDna(),
            Feedback = "Make it darker.",
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_feedback_text_missing()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "feedback",
            StoryDna = ValidDna(),
            PreviousStory = "Some previous story content.",
            Feedback = null,
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_feedback_text_too_long()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "feedback",
            StoryDna = ValidDna(),
            PreviousStory = "Some previous story content.",
            Feedback = new string('a', 5001),
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        await Assert.ThrowsAsync<StoryForgeException>(() => _sut.GenerateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Succeeds_for_valid_feedback_request()
    {
        var request = new GenerateStoryRequest
        {
            Mode = "feedback",
            StoryDna = ValidDna(),
            PreviousStory = "Some previous story content.",
            Feedback = "Make the ending darker.",
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        };

        var result = await _sut.GenerateAsync(request, CancellationToken.None);

        Assert.Equal("Title", result.Title);
    }
}
