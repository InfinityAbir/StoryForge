using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StoryForge.Api.Configuration;
using StoryForge.Api.Models;
using StoryForge.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
builder.Services.Configure<StoryOptions>(builder.Configuration.GetSection(StoryOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

var groqOptions = builder.Configuration.GetSection(GroqOptions.SectionName).Get<GroqOptions>() ?? new GroqOptions();
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
var rateLimitOptions = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

// ---- Request size limits ----
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 1024 * 1024; // 1 MB upper bound for JSON payload (story + previousStory, Bangla up to 3 bytes/char)
});

// ---- CORS ----
const string CorsPolicyName = "StoryForgeCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST");
        }
    });
});

// ---- Rate limiting ----
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = "RATE_LIMITED",
                Message = "You're generating stories a little too quickly. Please try again in a moment."
            }
        }, cancellationToken);
    };

    options.AddPolicy("story-generate", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rateLimitOptions.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
            SegmentsPerWindow = 4,
            QueueLimit = rateLimitOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

// ---- HttpClient for Groq ----
builder.Services.AddHttpClient<IGroqService, GroqService>(client =>
{
    client.BaseAddress = new Uri(groqOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(groqOptions.TimeoutSeconds);
});

builder.Services.AddScoped<IStoryService, StoryService>();

var app = builder.Build();

app.UseHttpsRedirection();

// ---- Security headers ----
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseCors(CorsPolicyName);
app.UseRateLimiter();

// ---- Global exception handling ----
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (StoryForgeException ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogWarning("Handled StoryForgeException {Code} with status {Status}", ex.Code, ex.StatusCode);

        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Error = new ApiError { Code = ex.Code, Message = ex.Message }
        });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Error = new ApiError { Code = "INTERNAL_ERROR", Message = "Something went wrong. Please try again." }
        });
    }
});

// ---- Endpoints ----
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/story/generate", async (
        GenerateStoryRequest request,
        IStoryService storyService,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;
        logger.LogInformation("Generation request started requestId={RequestId} mode={Mode}", correlationId, request.Mode);

        var result = await storyService.GenerateAsync(request, cancellationToken);

        var durationMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
        logger.LogInformation(
            "Generation request completed requestId={RequestId} mode={Mode} durationMs={DurationMs} status=success",
            correlationId, request.Mode, durationMs);

        return Results.Ok(result);
    })
    .RequireRateLimiting("story-generate");

app.Run();

public partial class Program;
