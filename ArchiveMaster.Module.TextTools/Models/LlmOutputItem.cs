namespace ArchiveMaster.Models;

public class LlmOutputItem : ICheckItem
{
    public LlmOutputItem()
    {
    }

    public LlmOutputItem(string text)
    {
        Text = text;
    }

    public string Text { get; set; }
    
    public static implicit operator LlmOutputItem(string text) => new LlmOutputItem(text);

    public static implicit operator string(LlmOutputItem item) => item.Text;
}
