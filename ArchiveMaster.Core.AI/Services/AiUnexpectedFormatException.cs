namespace ArchiveMaster.Services;

public class AiUnexpectedFormatException : Exception
{
    public AiUnexpectedFormatException()
    {
        
    }

    public AiUnexpectedFormatException(string message):base(message)
    {
        
    }

    public AiUnexpectedFormatException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}