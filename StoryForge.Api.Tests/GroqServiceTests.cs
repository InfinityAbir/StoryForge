using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StoryForge.Api.Configuration;
using StoryForge.Api.Models;
using StoryForge.Api.Services;
using Xunit;

namespace StoryForge.Api.Tests;

public class GroqServiceTests
{
    private static GenerateStoryRequest InitialRequest() => new()
    {
        Mode = "initial",
        Story = new string('a', 150),
        InputLanguage = "auto",
        OutputLanguage = "same",
        Length = "medium"
    };

    private static GroqService CreateSut(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://fake-groq.test/") };
        var options = Options.Create(new GroqOptions { ApiKey = "test-key", Model = "test-model" });
        return new GroqService(httpClient, options, NullLogger<GroqService>.Instance);
    }

    private const string ValidPayload = """
        {
          "title": "The Last Lantern",
          "story": "Once there was a humble artisan who found something extraordinary and it changed everything about how they saw the world around them for good and bad.",
          "storyDna": {
            "genre": ["Fantasy"],
            "themes": ["Greed", "Family"],
            "tone": ["Dark", "Emotional"],
            "protagonistArchetype": "Ordinary person",
            "centralConflict": "Temptation",
            "emotionalArc": ["Hope", "Loss"],
            "structure": ["Discovery", "Consequence"],
            "endingType": "Bittersweet"
          }
        }
        """;

    private static string WrapAsGroqResponse(string content, string finishReason = "stop") => $$"""
        {
          "choices": [
            {
              "message": { "content": {{System.Text.Json.JsonSerializer.Serialize(content)}} },
              "finish_reason": {{System.Text.Json.JsonSerializer.Serialize(finishReason)}}
            }
          ]
        }
        """;

    [Fact]
    public async Task Returns_parsed_response_for_valid_groq_output()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse(ValidPayload), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var result = await sut.GenerateAsync(InitialRequest(), CancellationToken.None);

        Assert.Equal("The Last Lantern", result.Title);
        Assert.Single(result.StoryDna.Genre);
    }

    [Fact]
    public async Task Throws_invalid_output_for_malformed_json()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse("{ this is not valid json"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("INVALID_AI_OUTPUT", ex.Code);
    }

    [Fact]
    public async Task Throws_invalid_output_when_story_field_missing()
    {
        const string payload = """{ "title": "Title only", "storyDna": null }""";
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse(payload), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("INVALID_AI_OUTPUT", ex.Code);
    }

    [Fact]
    public async Task Throws_invalid_output_when_story_dna_arrays_empty()
    {
        const string payload = """
            {
              "title": "Title",
              "story": "A reasonably long story body that exceeds the minimum character threshold for validity.",
              "storyDna": {
                "genre": [],
                "themes": ["Greed"],
                "tone": ["Dark"],
                "protagonistArchetype": "Everyman",
                "centralConflict": "Temptation",
                "emotionalArc": ["Hope"],
                "structure": ["Discovery"],
                "endingType": "Bittersweet"
              }
            }
            """;
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse(payload), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("INVALID_AI_OUTPUT", ex.Code);
    }

    [Fact]
    public async Task Throws_provider_refusal_on_content_filter_finish_reason()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse("", "content_filter"), Encoding.UTF8, "application/json")
            });
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("PROVIDER_REFUSAL", ex.Code);
    }

    [Fact]
    public async Task Throws_generation_failed_on_persistent_server_error()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("GENERATION_FAILED", ex.Code);
    }

    [Fact]
    public async Task Retries_once_on_transient_error_then_succeeds()
    {
        var callCount = 0;
        var handler = new FakeHttpMessageHandler((_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(WrapAsGroqResponse(ValidPayload), Encoding.UTF8, "application/json")
            };
        });
        var sut = CreateSut(handler);

        var result = await sut.GenerateAsync(InitialRequest(), CancellationToken.None);

        Assert.Equal(2, callCount);
        Assert.Equal("The Last Lantern", result.Title);
    }

    [Fact]
    public async Task Throws_generation_failed_on_rate_limited_response()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var sut = CreateSut(handler);

        var ex = await Assert.ThrowsAsync<StoryForgeException>(() => sut.GenerateAsync(InitialRequest(), CancellationToken.None));
        Assert.Equal("GENERATION_FAILED", ex.Code);
    }

    private class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request, cancellationToken));
        }
    }
}
