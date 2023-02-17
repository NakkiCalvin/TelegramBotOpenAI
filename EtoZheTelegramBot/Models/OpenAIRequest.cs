using Newtonsoft.Json;

namespace EtoZheTelegramBot.Models;

internal sealed class OpenAIRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = string.Empty;

    [JsonProperty("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonProperty("temperature")]
    public int Temperature { get; set; }

    [JsonProperty("max_tokens")]
    public int MaxTokens { get; set; }
}
