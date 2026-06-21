using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("chat")]
public class ChatController : ControllerBase
{
    private const int MaxMessageLength = 2000;

    private readonly OpenAiService _openAi;

    public ChatController(OpenAiService openAi)
    {
        _openAi = openAi;
    }

    [HttpGet("welcome")]
    [DisableRateLimiting]
    public IActionResult GetWelcome()
    {
        return Ok(new { message = ChatWelcome.Message });
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

        var reply = await _openAi.Ask(message);
        return Ok(new { reply });
    }
}

public class ChatRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}
