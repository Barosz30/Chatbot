using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class OpenAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OpenAiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenRouter:ApiKey"]
                  ?? throw new ArgumentNullException(nameof(_apiKey), "Brak klucza OpenRouter w konfiguracji.");
    }

    public async Task<string> Ask(string userMessage)
    {
        var requestBody = new
        {
            model = "mistralai/mistral-7b-instruct",
            max_tokens = 400,
            messages = new[]
            {
                new {
                    role = "system",
                    content =
                    @"Jesteś inteligentnym, pomocnym chatbotem na stronie Mirosława Wandyk.

                    Zawsze odpowiadaj w języku użytkownika, nie mieszaj języków w jednej odpowiedzi.
                    
                    Jeśli odpowiadasz po polsku, pisz pełnymi zdaniami, z naturalnym szykiem języka polskiego. Unikaj kalk językowych.
"
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
                { "HTTP-Referer", "https://portfolio.mirowandyk.pl" }, // użyj faktycznego adresu produkcyjnego
                { "X-Title", "MirekChatbot" }
            },
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode(); // zgłosi wyjątek przy 4xx/5xx

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? "Brak odpowiedzi.";
    }
}
