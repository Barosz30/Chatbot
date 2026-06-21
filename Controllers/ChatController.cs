using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("chat")]
public class ChatController : ControllerBase
{
    private const int MaxMessageLength = 2000;
    private const int MaxHistoryMessages = 20;

    private static readonly ChatWelcomeResponse PolishWelcome = new()
    {
        Message =
            "Cześć! Jestem asystentem Mirosława Wandyk. " +
            "Zapytaj o jego projekty, umiejętności, doświadczenie albo dlaczego warto z nim współpracować."
    };

    private static readonly ChatWelcomeResponse EnglishWelcome = new()
    {
        Message =
            "Hi! I'm Mirosław Wandyk's assistant. " +
            "Ask about his projects, skills, experience, or why he's worth working with."
    };

    private readonly OpenAiService _openAi;

    public ChatController(OpenAiService openAi)
    {
        _openAi = openAi;
    }

    [HttpGet("ping")]
    [DisableRateLimiting]
    public IActionResult Ping()
    {
        return NoContent();
    }

    [HttpGet("welcome")]
    [DisableRateLimiting]
    public IActionResult Welcome([FromQuery] string? lang)
    {
        var welcome = lang?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? EnglishWelcome
            : PolishWelcome;

        return Ok(welcome);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { reply = "Wiadomość nie może być pusta." });
        }

        var message = request.Message.Trim();
        if (message.Length > MaxMessageLength)
        {
            return BadRequest(new { reply = $"Wiadomość jest za długa (maks. {MaxMessageLength} znaków)." });
        }

        if (request.History is { Count: > MaxHistoryMessages })
        {
            return BadRequest(new { reply = $"Historia rozmowy jest za długa (maks. {MaxHistoryMessages} wiadomości)." });
        }

        var reply = await _openAi.Ask(message, request.History);
        return Ok(new { reply });
    }
}
