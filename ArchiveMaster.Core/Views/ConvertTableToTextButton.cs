using System.Collections;
using ArchiveMaster.Enums;
using ArchiveMaster.Helpers;
using ArchiveMaster.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FzLib.Avalonia.Converters;

namespace ArchiveMaster.Views;

public class ConvertTableToTextButton : Button
{
    public static readonly StyledProperty<IEnumerable> ItemsSourceProperty =
        AvaloniaProperty.Register<ConvertTableToTextButton, IEnumerable>(
            nameof(ItemsSource));

    public static readonly StyledProperty<object> PropertiesProperty =
        AvaloniaProperty.Register<ConvertTableToTextButton, object>(
            nameof(Properties));

    public IEnumerable ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Type ItemType { get; set; }
    public object Properties
    {
        get => GetValue(PropertiesProperty);
        set => SetValue(PropertiesProperty, value);
    }

    protected override Type StyleKeyOverride { get; } = typeof(Button);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var menu = new MenuFlyout();
        var values = Enum.GetValues<TableTextType>();
        foreach (var value in values)
        {
            var desc = DescriptionConverter.GetDescription(value);
            var item = new MenuItem() { Header = desc, Tag = value };
            item.Click += async (sender, args) =>
            {
                var type = ItemType ?? throw new ArgumentNullException(nameof(ItemType));
                TableTextConverter converter = new TableTextConverter(type, GetProperties());
                var m = sender as MenuItem ?? throw new ArgumentNullException(nameof(sender));
                var exportType = m.Tag is TableTextType tag ? tag : TableTextType.TabDelimited;
                var text = converter.ConvertToTableText(ItemsSource, exportType);
                if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
                {
                    if (topLevel.Clipboard != null)
                    {
                        await topLevel.Clipboard.SetTextAsync(text);
                    }
                }
                else
                {
                    throw new InvalidOperationException("无法获取顶级窗口");
                }
            };
            menu.Items.Add(item);
        }

        Flyout = menu;
    }

    private IList<PropertyFieldInfo> GetProperties()
    {
        if (Properties == null)
        {
            return null;
        }

        if (Properties is IEnumerable<PropertyFieldInfo> properties)
        {
            return properties.ToList();
        }

        if (Properties is string str)
        {
            return str.Split(',').Select(p => new PropertyFieldInfo()
            {
                PropertyName = p
            }).ToList();
        }

        throw new ArgumentException($"无法解析属性：{Properties}");
    }
}