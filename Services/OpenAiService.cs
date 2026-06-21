using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class OpenAiService
{
    private static readonly string[] DefaultOpenRouterModels =
    {
        "qwen/qwen3-next-80b-a3b-instruct:free",
        "meta-llama/llama-3.3-70b-instruct:free",
        "cognitivecomputations/dolphin-mistral-24b-venice-edition:free",
        "openai/gpt-oss-20b:free"
    };

    private static readonly string[] DefaultGroqModels =
    {
        "llama-3.3-70b-versatile",
        "llama-3.1-8b-instant"
    };

    private const int MaxHistoryMessages = 20;

    private readonly HttpClient _httpClient;
    private readonly ChatKnowledgeService _knowledge;
    private readonly IReadOnlyList<ChatCompletionProvider> _providers;
    private readonly int _maxTokens;

    public OpenAiService(HttpClient httpClient, IConfiguration config, ChatKnowledgeService knowledge)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _knowledge = knowledge;
        _maxTokens = config.GetValue<int?>("OpenRouter:MaxTokens") ?? 600;
        _providers = BuildProviders(config);
    }

    public async Task<string> Ask(string userMessage, IReadOnlyList<ChatHistoryMessage>? history)
    {
        var messages = BuildMessages(userMessage, history);

        foreach (var provider in _providers)
        {
            foreach (var model in provider.Models)
            {
                try
                {
                    var reply = await SendCompletionRequest(provider, model, messages);
                    if (reply is not null && IsUsableReply(reply))
                    {
                        return reply!;
                    }

                    Console.WriteLine($"Odrzucono odpowiedź ({provider.Name}/{model}): {reply}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd ({provider.Name}/{model}): {ex.Message}");
                }
            }
        }

        return "Przepraszam, nie udało mi się teraz wygenerować odpowiedzi. Spróbuj ponownie za chwilę.";
    }

    private static IReadOnlyList<ChatCompletionProvider> BuildProviders(IConfiguration config)
    {
        var providers = new List<ChatCompletionProvider>();

        var groqApiKey = config["Groq:ApiKey"]?.Trim();
        var groqModels = config.GetSection("Groq:Models").Get<string[]>();
        if (!string.IsNullOrWhiteSpace(groqApiKey))
        {
            providers.Add(new ChatCompletionProvider(
                "Groq",
                "https://api.groq.com/openai/v1/chat/completions",
                groqApiKey,
                groqModels is { Length: > 0 } ? groqModels : DefaultGroqModels,
                null));
        }

        var openRouterApiKey = config["OpenRouter:ApiKey"]?.Trim();
        if (string.IsNullOrWhiteSpace(openRouterApiKey))
        {
            if (providers.Count == 0)
            {
                throw new ArgumentNullException(
                    nameof(openRouterApiKey),
                    "Brak klucza OpenRouter w konfiguracji.");
            }
        }
        else
        {
            var openRouterModels = config.GetSection("OpenRouter:Models").Get<string[]>();
            providers.Add(new ChatCompletionProvider(
                "OpenRouter",
                "https://openrouter.ai/api/v1/chat/completions",
                openRouterApiKey,
                openRouterModels is { Length: > 0 } ? openRouterModels : DefaultOpenRouterModels,
                new Dictionary<string, string>
                {
                    ["HTTP-Referer"] = "https://portfolio.mirowandyk.pl",
                    ["X-Title"] = "MirekChatbot"
                }));
        }

        return providers;
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

    private async Task<string?> SendCompletionRequest(
        ChatCompletionProvider provider,
        string model,
        object[] messages)
    {
        var requestBody = new
        {
            model,
            max_tokens = _maxTokens,
            temperature = 0.4,
            messages
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, provider.EndpointUrl)
        {
            Headers = { { "Authorization", $"Bearer {provider.ApiKey}" } },
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (provider.Headers is not null)
        {
            foreach (var (name, value) in provider.Headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"HTTP {(int)response.StatusCode} ({provider.Name}/{model}): {TruncateForLog(content)}");
            return null;
        }

        using var doc = JsonDocument.Parse(content);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        if (message.TryGetProperty("content", out var contentElement))
        {
            return contentElement.GetString();
        }

        Console.WriteLine($"Brak pola content ({provider.Name}/{model}): {TruncateForLog(content)}");
        return null;
    }

    private static string TruncateForLog(string value, int maxLength = 400)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        var normalized = value.Replace('\n', ' ').Replace('\r', ' ');
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
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

    private sealed record ChatCompletionProvider(
        string Name,
        string EndpointUrl,
        string ApiKey,
        string[] Models,
        Dictionary<string, string>? Headers);
}
