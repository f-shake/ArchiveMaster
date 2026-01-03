using Avalonia.Data.Converters;

namespace ArchiveMaster.Views;

public class SimpleFileRowDetailItem
{
    public static double DefaultLabelWidth = 120;

    public SimpleFileRowDetailItem()
    {
    }

    public SimpleFileRowDetailItem(string label, string bindingPath, IValueConverter converter = null)
    {
        Label = label;
        BindingPath = bindingPath;
        Converter = converter;
    }

    public string Label { get; set; }

    public double LabelWidth { get; set; } = DefaultLabelWidth;

    public string BindingPath { get; set; }

    public IValueConverter Converter { get; set; }
}