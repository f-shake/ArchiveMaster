using Avalonia.Controls;

namespace ArchiveMaster.Views;

public class FileDataGridRowDetailDefaultContent : ContentControl
{
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Content == null)
        {
            Content = DataContext;
        }
    }
}