using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ArchiveMaster.Behaviors;

public static class TextBlockInlinesBehavior
{
    public static readonly AttachedProperty<IEnumerable<InlineItem>> ItemsSourceProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IEnumerable<InlineItem>>(
            "ItemsSource", typeof(TextBlockInlinesBehavior));

    public static IEnumerable<InlineItem> GetItemsSource(TextBlock element) =>
        element.GetValue(ItemsSourceProperty);

    public static void SetItemsSource(TextBlock element, IEnumerable<InlineItem> value) =>
        element.SetValue(ItemsSourceProperty, value);

    static TextBlockInlinesBehavior()
    {
        ItemsSourceProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is TextBlock textBlock && args.NewValue.Value is IEnumerable<InlineItem> items)
            {
                UpdateInlines(textBlock, items);
            }
        });
    }

    private static void UpdateInlines(TextBlock textBlock, IEnumerable<InlineItem> items)
    {
        textBlock.Inlines.Clear();

        foreach (var item in items)
        {
            var run = new Run { Text = item.Text };

            if (item.IsBold)
            {
                run.FontWeight = FontWeight.Bold;
            }

            if (item.Foreground != null)
            {
                run.Foreground = item.Foreground;
            }

            textBlock.Inlines.Add(run);
        }
    }
}