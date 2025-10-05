namespace ArchiveMaster.Models;

public class OllamaRequestData
{
    public string Model { get; set; }
    public string Prompt { get; set; }
    public bool Stream { get; set; } = false;
    public string Format { get; set; } = string.Empty;
    public OllamaMessage[] Messages { get; set; }

    public class OllamaMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}
