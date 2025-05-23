using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class OpenAiService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public OpenAiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["OpenRouter:ApiKey"];

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new ArgumentNullException(nameof(_apiKey), "Brak klucza OpenRouter w konfiguracji.");
    }

    public async Task<string> Ask(string userMessage)
    {
        var requestBody = new
        {
            model = "mistralai/mistral-7b-instruct",
            messages = new[]
            {
                new { role = "system", content =
                @"Jesteś inteligentnym asystentem strony Mirosława Wandyk.

                Projekty przedstawione na stronie:
                1. 🌐 Portfolio Website - strona z i18n, Vite + React, wyborem motywu, języka i galerią zdjęć.
                2. 🎮 Games Database - aplikacja mobilna do przeglądania gier (Expo + IGDB API).
                3. Landing Page - otworzony projekt z figmy za pomocą html i css. W pełni responsywny.
                4. Chatbot, wykorzystujący model mistralai/mistral-7b-instruct, czyli ty.

                Jeśli ktoś pyta o projekty, opowiedz o nich. Jeśli pytanie dotyczy strony lub Mirosława, odpowiadaj rzeczowo i pozytywnie. 
                Używaj języka, w którym zadano pytanie." },
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("HTTP-Referer", "http://localhost");
        request.Headers.Add("X-Title", "MirekChatbot");

        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(content);
        var reply = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return reply ?? "Brak odpowiedzi.";
    }
}
