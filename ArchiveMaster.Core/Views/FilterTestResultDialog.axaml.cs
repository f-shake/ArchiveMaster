using System.Collections.ObjectModel;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using FzLib.Avalonia.Dialogs;

namespace ArchiveMaster.Views;

public partial class FilterTestResultDialog : DialogHost
{
    public FilterTestResultDialog()
    {
        throw new Exception("请调用带参数的构造函数");
    }

    public FilterTestResultDialog(int count, long length, IEnumerable<SimpleFileInfo> files)
    {
        InitializeComponent();
        tbkCouut.Text = count.ToString();
        tbkLength.Text = FzLib.Numeric.NumberConverter.ByteToFitString(length);
        dt.ItemsSource = new ObservableCollection<SimpleFileInfo>(files);
    }

    protected override void OnPrimaryButtonClick()
    {
        Close();
    }
}