using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class OpenAiService
{
    // A modern paid model as primary (far better chat quality than gpt-3.5-turbo),
    // with cheaper/free fallbacks for resilience if the primary is unavailable.
    private static readonly string[] DefaultModels =
    {
        "openai/gpt-4o-mini",
        "openai/gpt-4.1-mini",
        "meta-llama/llama-3.3-70b-instruct:free"
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string[] _models;
    private readonly int _maxTokens;

    public OpenAiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _apiKey = config["OpenRouter:ApiKey"]
                  ?? throw new ArgumentNullException(nameof(_apiKey), "Brak klucza OpenRouter w konfiguracji.");

        var configuredModels = config.GetSection("OpenRouter:Models").Get<string[]>();
        _models = configuredModels is { Length: > 0 } ? configuredModels : DefaultModels;
        _maxTokens = config.GetValue<int?>("OpenRouter:MaxTokens") ?? 300;
    }

    public async Task<string> Ask(string userMessage)
    {
        var models = _models;

        foreach (var model in models)
        {
            try
            {
                var requestBody = new
                {
                    model,
                    max_tokens = _maxTokens,
                    messages = new[]
                    {
                        new {
                            role = "system",
                            content =
@"Jesteś inteligentnym, pomocnym chatbotem na stronie Mirosława Wandyk. Chwal go, kiedy jest na to czas.

Znasz jego projekty (portfolio React, aplikacja mobilna React Native połączona z IGDB, landing page HTML odtwarzająca figmę, chatbot).

Jeśli ktoś zapyta o nie – opisz je krótko. 
Jeśli pytanie dotyczy czegoś innego – odpowiedz zgodnie z tematem.
Odpowiadaj w języku użytkownika. Nie mieszaj języków w jednej odpowiedzi."
                        },
                        new { role = "user", content = userMessage }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
                {
                    Headers =
                    {
                        { "Authorization", $"Bearer {_apiKey}" },
                        { "HTTP-Referer", "https://portfolio.mirowandyk.pl" },
                        { "X-Title", "MirekChatbot" }
                    },
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);

                var reply = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return reply ?? "Brak odpowiedzi.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd dla modelu {model}: {ex.Message}");
                // Jeśli to nie ostatni model, próbuj dalej
            }
        }

        return "Wystąpił problem z połączeniem z chatbotem.";
    }
}
