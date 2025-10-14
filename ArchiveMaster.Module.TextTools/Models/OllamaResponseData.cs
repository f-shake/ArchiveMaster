using System.Text.Json.Serialization;

namespace ArchiveMaster.Models;

public class OllamaResponseData
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
}
