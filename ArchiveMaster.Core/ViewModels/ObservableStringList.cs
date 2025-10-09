using System.Collections.ObjectModel;

namespace ArchiveMaster.ViewModels;

public class ObservableStringList : ObservableCollection<EditableString>
{
    public ObservableStringList()
    {
    }

    public ObservableStringList(IEnumerable<EditableString> collection)
        : base(collection)
    {
    }

    public ObservableStringList(List<EditableString> list)
        : base(list)
    {
    }
    
    public ObservableStringList(IEnumerable<string> collection)
        : base(collection.Select(s => new EditableString(s)))
    {
    }

    public ObservableStringList(List<string> list)
        : base(list.Select(s => new EditableString(s)).ToList())
    {
    }
    
    public IEnumerable<string> Trimmed => this.Select(s => s.Value).Where(s => !string.IsNullOrWhiteSpace(s));
}