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
            max_tokens = 400, // ✅ ograniczamy długość odpowiedzi
            messages = new[]
            {
                new {
                    role = "system",
                    content =
@"Jesteś inteligentnym, pomocnym chatbotem na stronie Mirosława Wandyk.

Znane projekty:
1. 🌐 Portfolio Website – Vite + React + i18n, wybór motywu i języka, galeria zdjęć.
2. 🎮 Games Database – mobilna aplikacja (Expo + IGDB API) do przeglądania gier.
3. 📄 Landing Page – statyczna strona z Figmy w HTML i CSS, w pełni responsywna.
4. 🤖 Chatbot – oparty na modelu mistral-7b-instruct (czyli Ty).

Jeśli ktoś pyta o projekty, opisz je rzeczowo. Jeśli pytanie dotyczy strony lub Mirosława – odpowiedz pozytywnie, ale konkretnie.
Odpowiadaj w języku użytkownika i nie mieszaj języków w jednej odpowiedzi."
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
