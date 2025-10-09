using System.Collections.ObjectModel;

namespace ArchiveMaster.ViewModels;

public class ObservableStringList : ObservableCollection<EditableString>
{
    public IEnumerable<string> Trimmed => this.Select(s => s.Value).Where(s => !string.IsNullOrWhiteSpace(s));
}