using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StoryForge.Api.Models;
using StoryForge.Api.Services;
using Xunit;

namespace StoryForge.Api.Tests;

public class IntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGroqService>();
                services.AddSingleton<IGroqService, StubGroqService>();
            });
        });
    }

    [Fact]
    public async Task Health_endpoint_returns_ok_without_calling_groq()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task Health_response_does_not_leak_configuration()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("ApiKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gsk_", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Generate_returns_400_for_invalid_mode()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/story/generate", new
        {
            mode = "not-a-mode",
            inputLanguage = "auto",
            outputLanguage = "same",
            length = "medium"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", body?.Error.Code);
    }

    [Fact]
    public async Task Generate_returns_400_for_story_too_short()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/story/generate", new GenerateStoryRequest
        {
            Mode = "initial",
            Story = "too short",
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_returns_200_with_story_dna_for_valid_initial_request()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/story/generate", new GenerateStoryRequest
        {
            Mode = "initial",
            Story = new string('a', 150),
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GenerateStoryResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body?.Title));
        Assert.NotEmpty(body!.StoryDna.Genre);
    }

    [Fact]
    public async Task Generate_does_not_expose_internal_error_details_on_provider_failure()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGroqService>();
                services.AddSingleton<IGroqService>(new ThrowingGroqService());
            });
        });
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/story/generate", new GenerateStoryRequest
        {
            Mode = "initial",
            Story = new string('a', 150),
            InputLanguage = "auto",
            OutputLanguage = "same",
            Length = "medium"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at StoryForge.Api", raw, StringComparison.OrdinalIgnoreCase);
    }

    private record HealthResponse(string Status);

    private class StubGroqService : IGroqService
    {
        public Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new GenerateStoryResponse
            {
                Title = "Stub Title",
                Story = "Stub story content used for integration testing purposes only.",
                StoryDna = new StoryDna
                {
                    Genre = ["Fantasy"],
                    Themes = ["Greed"],
                    Tone = ["Dark"],
                    ProtagonistArchetype = "Everyman",
                    CentralConflict = "Temptation",
                    EmotionalArc = ["Hope", "Loss"],
                    Structure = ["Discovery", "Consequence"],
                    EndingType = "Bittersweet"
                }
            });
        }
    }

    private class ThrowingGroqService : IGroqService
    {
        public Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated unexpected failure with sensitive internal detail.");
        }
    }
}
