using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton<ChatKnowledgeService>();
builder.Services.AddHttpClient<OpenAiService>();

// The app runs behind Render's reverse proxy, so the real client IP arrives in
// the X-Forwarded-For header. Honour it so rate limiting partitions per client.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-IP rate limiting protects the (paid) upstream model from abuse and runaway
// cost, since the public /api/chat endpoint has no authentication.
var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 10;
var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("chat", httpContext =>
    {
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"reply\":\"Za dużo zapytań. Spróbuj ponownie za chwilę.\"}", token);
    };
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "https://barosz30.github.io",
                "https://portfolio.mirowandyk.pl",
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

var groqKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Groq:ApiKey"]);
var openRouterKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["OpenRouter:ApiKey"]);

if (!groqKeyConfigured && !openRouterKeyConfigured)
{
    app.Logger.LogWarning(
        "Brak kluczy LLM. Ustaw Groq__ApiKey lub OpenRouter__ApiKey w Render Environment.");
}
else
{
    app.Logger.LogInformation(
        "LLM providers: Groq={GroqConfigured}, OpenRouter={OpenRouterConfigured}",
        groqKeyConfigured,
        openRouterKeyConfigured);
}

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild",
    "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
