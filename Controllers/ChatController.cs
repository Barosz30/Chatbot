using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly OpenAiService _openAi;

    public ChatController(OpenAiService openAi)
    {
        _openAi = openAi;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request)
    {
        var reply = await _openAi.Ask(request.Message);
        return Ok(new { reply });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}
