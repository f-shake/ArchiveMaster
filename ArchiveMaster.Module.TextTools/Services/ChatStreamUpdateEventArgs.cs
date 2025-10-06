namespace ArchiveMaster.Services;

public class ChatStreamUpdateEventArgs(string text) : EventArgs
{
    public string Text { get; set; } = text;
}