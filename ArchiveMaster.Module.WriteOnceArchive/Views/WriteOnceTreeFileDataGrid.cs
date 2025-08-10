using ArchiveMaster.ViewModels.FileSystem;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ArchiveMaster.Views;

public class WriteOnceTreeFileDataGrid : TreeFileDataGrid
{
    public string FileNameHalfTransparentBindingPath { get; set; }

    protected override Control GetNameContentTemplate()
    {
        var tbkName = new TextBlock()
        {
            [!TextBlock.TextProperty] = new Binding(nameof(SimpleFileInfo.Name)),
        };
        if (FileNameHalfTransparentBindingPath != null)
        {
            tbkName[!OpacityProperty] = new Binding($"{nameof(TreeFileDirInfo.RawFileInfo)}.{FileNameHalfTransparentBindingPath}")
            {
                Converter = new FuncValueConverter<bool, double>(p => p ? 0.5 : 1)
            };
        }

        return tbkName;
    }
}