namespace ArchiveMaster.Models;

public class PromptItem : ICheckItem
{
    public PromptItem()
    {
    }

    public PromptItem(string prompt)
    {
        Prompt = prompt;
    }

    public string Prompt { get; set; }
}