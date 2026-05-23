namespace ArchiveMaster.Events;

public class AiAssistantEndLineEventArgs(string lineText) : EventArgs
{
    public string LineText { get; private set; } = lineText;
    
    public void ReplaceLine(string lineText)
    {
        LineText = lineText;
    }
}