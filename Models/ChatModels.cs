using System.ComponentModel.DataAnnotations;

public class ChatRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;

    public List<ChatHistoryMessage>? History { get; set; }
}

public class ChatHistoryMessage
{
    [Required]
    [RegularExpression("^(user|assistant)$")]
    public string Role { get; set; } = string.Empty;

    [Required]
    [StringLength(4000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;
}

public class ChatWelcomeResponse
{
    public string Message { get; set; } = string.Empty;
}

public class ChatStatusResponse
{
    public bool GroqKeyConfigured { get; set; }

    public bool OpenRouterKeyConfigured { get; set; }

    public string[] Providers { get; set; } = Array.Empty<string>();

    public string[] GroqModels { get; set; } = Array.Empty<string>();

    public string[] OpenRouterModels { get; set; } = Array.Empty<string>();
}
