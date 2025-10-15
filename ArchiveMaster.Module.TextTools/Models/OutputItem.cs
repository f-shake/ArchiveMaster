namespace ArchiveMaster.Models;

public class OutputItem : ICheckItem
{
    public OutputItem()
    {
    }

    public OutputItem(string message)
    {
        Message = message;
    }

    public string Message { get; set; }
}
