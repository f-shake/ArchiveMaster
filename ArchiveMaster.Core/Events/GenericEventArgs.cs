namespace ArchiveMaster.Events;

public class GenericEventArgs<T> : EventArgs
{
    public GenericEventArgs(T value)
    {
        Value = value;
    }

    public GenericEventArgs()
    {
    }

    public T Value { get; init; }
}