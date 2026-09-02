using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StoryForge.Api.Configuration;
using StoryForge.Api.Models;
using StoryForge.Api.Prompts;

namespace StoryForge.Api.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqService> _logger;
    private readonly GroqOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GroqService(HttpClient httpClient, IOptions<GroqOptions> options, ILogger<GroqService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<GenerateStoryResponse> GenerateAsync(GenerateStoryRequest request, CancellationToken cancellationToken)
    {
        var groqRequest = new GroqChatRequest
        {
            Model = _options.Model,
            Messages =
            [
                new GroqMessage { Role = "system", Content = StoryPrompts.SystemPrompt },
                new GroqMessage { Role = "user", Content = StoryPrompts.BuildUserMessage(request) }
            ],
            ResponseFormat = new GroqResponseFormat { Type = "json_object" },
            Temperature = 0.9,
            MaxCompletionTokens = 16000
        };

        GroqChatResponse? groqResponse;
        try
        {
            groqResponse = await SendWithRetryAsync(groqRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Groq request timed out");
            throw StoryForgeException.Timeout();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Groq request failed");
            throw StoryForgeException.GenerationFailed();
        }

        var choice = groqResponse?.Choices?.FirstOrDefault();
        if (choice is null)
        {
            _logger.LogWarning("Groq response contained no choices");
            throw StoryForgeException.GenerationFailed();
        }

        if (string.Equals(choice.FinishReason, "content_filter", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Groq refused request due to content filter");
            throw StoryForgeException.ProviderRefusal();
        }

        var content = choice.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Groq response content was empty");
            throw StoryForgeException.InvalidOutput();
        }

        return ParseAndValidate(content);
    }

    private async Task<GroqChatResponse?> SendWithRetryAsync(GroqChatRequest groqRequest, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(groqRequest, options: JsonOptions)
            };
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GroqChatResponse>(JsonOptions, cancellationToken);
            }

            var transient = response.StatusCode is HttpStatusCode.InternalServerError
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout;

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Groq rate limit hit");
                throw StoryForgeException.GenerationFailed();
            }

            if (!transient || attempt == maxAttempts)
            {
                _logger.LogWarning("Groq returned non-success status {StatusCode}", (int)response.StatusCode);
                throw StoryForgeException.GenerationFailed();
            }
            // transient failure, loop to retry once
        }

        throw StoryForgeException.GenerationFailed();
    }

    private GenerateStoryResponse ParseAndValidate(string content)
    {
        AiGeneratedPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<AiGeneratedPayload>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Groq JSON output");
            throw StoryForgeException.InvalidOutput();
        }

        if (payload is null)
        {
            throw StoryForgeException.InvalidOutput();
        }

        if (string.IsNullOrWhiteSpace(payload.Title) || payload.Title.Length > 200)
        {
            throw StoryForgeException.InvalidOutput();
        }

        if (string.IsNullOrWhiteSpace(payload.Story) || payload.Story.Length < 50 || payload.Story.Length > 80000)
        {
            throw StoryForgeException.InvalidOutput();
        }

        var dna = payload.StoryDna;
        if (dna is null)
        {
            throw StoryForgeException.InvalidOutput();
        }

        ValidateDnaList(dna.Genre, 1, 5);
        ValidateDnaList(dna.Themes, 1, 8);
        ValidateDnaList(dna.Tone, 1, 6);
        ValidateDnaList(dna.EmotionalArc, 1, 10);
        ValidateDnaList(dna.Structure, 1, 10);

        if (string.IsNullOrWhiteSpace(dna.ProtagonistArchetype) || dna.ProtagonistArchetype.Length > 200)
        {
            throw StoryForgeException.InvalidOutput();
        }

        if (string.IsNullOrWhiteSpace(dna.CentralConflict) || dna.CentralConflict.Length > 300)
        {
            throw StoryForgeException.InvalidOutput();
        }

        if (string.IsNullOrWhiteSpace(dna.EndingType) || dna.EndingType.Length > 200)
        {
            throw StoryForgeException.InvalidOutput();
        }

        return new GenerateStoryResponse
        {
            Title = payload.Title.Trim(),
            Story = payload.Story.Trim(),
            StoryDna = dna
        };
    }

    private void ValidateDnaList(List<string>? list, int min, int max)
    {
        if (list is null || list.Count < min || list.Count > max || list.Any(string.IsNullOrWhiteSpace) || list.Any(s => s.Length > 100))
        {
            throw StoryForgeException.InvalidOutput();
        }
    }

    private class AiGeneratedPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Story { get; set; } = string.Empty;
        public StoryDna? StoryDna { get; set; }
    }

    private class GroqChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<GroqMessage> Messages { get; set; } = [];

        [JsonPropertyName("response_format")]
        public GroqResponseFormat? ResponseFormat { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.8;

        [JsonPropertyName("max_completion_tokens")]
        public int MaxCompletionTokens { get; set; } = 4000;
    }

    private class GroqMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class GroqResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private class GroqChatResponse
    {
        [JsonPropertyName("choices")]
        public List<GroqChoice>? Choices { get; set; }
    }

    private class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqResponseMessage? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class GroqResponseMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
