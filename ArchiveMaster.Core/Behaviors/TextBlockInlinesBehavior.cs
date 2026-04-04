using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.Collections;
using System.Collections.Specialized;
using ArchiveMaster.Models;
using Avalonia.Data;
using Avalonia.Threading;

namespace ArchiveMaster.Behaviors;

public static class TextBlockInlinesBehavior
{
    public static readonly AttachedProperty<IEnumerable<InlineItem>> ItemsSourceProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IEnumerable<InlineItem>>(
            "ItemsSource", typeof(TextBlockInlinesBehavior));

    private static readonly AttachedProperty<Dictionary<InlineItem, Inline>> InlineMapProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, Dictionary<InlineItem, Inline>>(
            "InlineMap", typeof(TextBlockInlinesBehavior));

    private static readonly AttachedProperty<NotifyCollectionChangedEventHandler>
        CollectionChangedHandlerProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, NotifyCollectionChangedEventHandler>(
                "CollectionChangedHandler", typeof(TextBlockInlinesBehavior));


    public static IEnumerable<InlineItem> GetItemsSource(TextBlock element) =>
        element.GetValue(ItemsSourceProperty);

    public static void SetItemsSource(TextBlock element, IEnumerable<InlineItem> value) =>
        element.SetValue(ItemsSourceProperty, value);

    static TextBlockInlinesBehavior()
    {
        ItemsSourceProperty.Changed.Subscribe(OnItemsSourceChanged);
    }

    private static void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs<IEnumerable<InlineItem>> args)
    {
        if (args.Sender is not TextBlock textBlock)
        {
            return;
        }

        var oldHandler = textBlock.GetValue(CollectionChangedHandlerProperty);

        if (oldHandler != null && args.OldValue.Value is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= oldHandler;
        }

        // 清理旧状态
        textBlock.ClearValue(CollectionChangedHandlerProperty);
        textBlock.Inlines.Clear();

        var inlineMap = new Dictionary<InlineItem, Inline>();
        textBlock.SetValue(InlineMapProperty, inlineMap);


        if (args.NewValue.Value is not IEnumerable<InlineItem> items)
        {
            return;
        }


        foreach (var item in items)
        {
            var inline = CreateInline(item);
            inlineMap[item] = inline;
            textBlock.Inlines.Add(inline);
        }

        // 增量监听
        if (items is INotifyCollectionChanged newCollection)
        {
            NotifyCollectionChangedEventHandler handler = (_, e) => OnCollectionChanged(textBlock, items, e);

            newCollection.CollectionChanged += handler;
            textBlock.SetValue(CollectionChangedHandlerProperty, handler);
        }
    }

    private static void OnCollectionChanged(
        TextBlock textBlock,
        IEnumerable<InlineItem> source,
        NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var map = textBlock.GetValue(InlineMapProperty);
            if (map == null)
            {
                return;
            }

            if (textBlock.Inlines == null)
            {
                textBlock.Inlines = new InlineCollection();
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    AddItems(textBlock, map, e.NewItems!, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    RemoveItems(textBlock, map, e.OldItems!);
                    AddItems(textBlock, map, e.NewItems!, e.NewStartingIndex);
                    break;


                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(textBlock, map, e.OldItems!);
                    break;

                case NotifyCollectionChangedAction.Move:
                    MoveItems(textBlock, map, e.OldItems!, e.OldStartingIndex, e.NewStartingIndex);
                    break;


                case NotifyCollectionChangedAction.Reset:
                    textBlock.Inlines.Clear();
                    map.Clear();
                    foreach (var item in source)
                    {
                        var inline = CreateInline(item);
                        map[item] = inline;
                        textBlock.Inlines.Add(inline);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        });
    }

    private static void MoveItems(
        TextBlock textBlock,
        Dictionary<InlineItem, Inline> map,
        IList items,
        int oldIndex,
        int newIndex)
    {
        if (items.Count != 1)
        {
            // AvaloniaList 通常只 move 一个
            foreach (InlineItem item in items)
            {
                if (!map.TryGetValue(item, out var inline))
                    continue;

                textBlock.Inlines.Remove(inline);
                textBlock.Inlines.Insert(newIndex, inline);
                newIndex++;
            }

            return;
        }

        var singleItem = (InlineItem)items[0];
        if (!map.TryGetValue(singleItem, out var singleRun))
            return;

        textBlock.Inlines.RemoveAt(oldIndex);
        textBlock.Inlines.Insert(newIndex, singleRun);
    }

    private static void AddItems(
        TextBlock textBlock,
        Dictionary<InlineItem, Inline> map,
        IList items,
        int startIndex)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = (InlineItem)items[i];
            var inline = CreateInline(item);
            map[item] = inline;

            int targetIndex = startIndex + i;

            if (targetIndex >= 0 && targetIndex <= textBlock.Inlines.Count)
            {
                textBlock.Inlines.Insert(targetIndex, inline);
            }
            else
            {
                textBlock.Inlines.Add(inline);
            }
        }
    }

    private static void RemoveItems(TextBlock textBlock, Dictionary<InlineItem, Inline> map, IList items)
    {
        foreach (InlineItem item in items)
        {
            if (map.TryGetValue(item, out var inline))
            {
                textBlock.Inlines.Remove(inline);
                map.Remove(item);
            }
        }
    }

    private static Inline CreateInline(InlineItem item)
    {
        if (item.Text == "\n")
        {
            return new LineBreak();
        }

        var run = new Run { Text = item.Text };
        if (item.IsBold)
        {
            run.FontWeight = FontWeight.Bold;
        }

        if (item.IsItalic)
        {
            run.FontStyle = FontStyle.Italic;
        }

        if (item.Foreground != null)
        {
            run.Foreground = item.Foreground;
        }

        if (item.FontSize > 0)
        {
            run.FontSize = item.FontSize;
        }

        return run;
    }
}