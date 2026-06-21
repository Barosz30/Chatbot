using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class OpenAiService
{
    // Pinned chat models only — avoid openrouter/free, which can route to moderation models.
    private static readonly string[] DefaultModels =
    {
        "meta-llama/llama-3.3-70b-instruct:free",
        "meta-llama/llama-3.2-3b-instruct:free"
    };

    private const int MaxHistoryMessages = 20;

    private readonly HttpClient _httpClient;
    private readonly ChatKnowledgeService _knowledge;
    private readonly string _apiKey;
    private readonly string[] _models;
    private readonly int _maxTokens;

    public OpenAiService(HttpClient httpClient, IConfiguration config, ChatKnowledgeService knowledge)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _knowledge = knowledge;
        _apiKey = config["OpenRouter:ApiKey"]
                  ?? throw new ArgumentNullException(nameof(_apiKey), "Brak klucza OpenRouter w konfiguracji.");

        var configuredModels = config.GetSection("OpenRouter:Models").Get<string[]>();
        _models = configuredModels is { Length: > 0 } ? configuredModels : DefaultModels;
        _maxTokens = config.GetValue<int?>("OpenRouter:MaxTokens") ?? 600;
    }

    public async Task<string> Ask(string userMessage, IReadOnlyList<ChatHistoryMessage>? history)
    {
        var messages = BuildMessages(userMessage, history);

        foreach (var model in _models)
        {
            try
            {
                var reply = await SendCompletionRequest(model, messages);
                if (IsUsableReply(reply))
                {
                    return reply!;
                }

                Console.WriteLine($"Odrzucono odpowiedź modelu {model}: {reply}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd dla modelu {model}: {ex.Message}");
            }
        }

        return "Przepraszam, nie udało mi się teraz wygenerować odpowiedzi. Spróbuj ponownie za chwilę.";
    }

    private object[] BuildMessages(string userMessage, IReadOnlyList<ChatHistoryMessage>? history)
    {
        var messages = new List<object>
        {
            new { role = "system", content = _knowledge.BuildSystemPrompt() }
        };

        if (history is { Count: > 0 })
        {
            var recentHistory = history
                .TakeLast(MaxHistoryMessages)
                .Where(message => message.Role is "user" or "assistant")
                .Where(message => !string.IsNullOrWhiteSpace(message.Content));

            foreach (var message in recentHistory)
            {
                messages.Add(new { role = message.Role, content = message.Content.Trim() });
            }
        }

        messages.Add(new { role = "user", content = userMessage });
        return messages.ToArray();
    }

    private async Task<string?> SendCompletionRequest(string model, object[] messages)
    {
        var requestBody = new
        {
            model,
            max_tokens = _maxTokens,
            temperature = 0.4,
            messages
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

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private static bool IsUsableReply(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return false;
        }

        var trimmed = reply.Trim();

        if (trimmed.Contains("User Safety", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("Response Safety", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}') &&
            trimmed.Contains("safe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
