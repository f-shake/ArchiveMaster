namespace ArchiveMaster.Events;

public class ChatStreamUpdateEventArgs(string text) : EventArgs
{
    public string Text { get; set; } = text;
}