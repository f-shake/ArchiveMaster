using System.Collections;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FzLib.Avalonia.Dialogs.Pickers;

namespace ArchiveMaster.Views
{
    public partial class StringListEditor : UserControl
    {
        public static readonly StyledProperty<ObservableStringList> ItemsSourceProperty = AvaloniaProperty.Register<StringListEditor, ObservableStringList>(
            nameof(ItemsSource));

        public ObservableStringList ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public StringListEditor()
        {
            InitializeComponent();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var list = GetItemsSourceList();
            list.Add("新项目");
        }

        private ObservableStringList GetItemsSourceList()
        {
            if (ItemsSource is null)
            {
                throw new InvalidOperationException("ItemsSource为空");
            }

            return ItemsSource;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var list = GetItemsSourceList();
            list.Clear();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            //string可能重复，通过获取准确index的形式进行删除
            // var index = items.IndexFromContainer(
            //     button.Parent /*StackPanel*/.Parent /*Border*/.Parent /*ContentPresenter*/ as Control);
            // var list = GetItemsSourceList();
            // list.RemoveAt(index);
            var editableString = button.DataContext as EditableString;
            if (editableString == null)
            {
                throw new InvalidOperationException("DataContext为空");
            }
            var list = GetItemsSourceList();
            list.Remove(editableString);
        }

        private void ScrollViewer_OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                // e.Delta.Y 是滚轮上下滚的值
                var offset = scroller.Offset;
                scroller.Offset = new Vector(offset.X - e.Delta.Y * 50, offset.Y);
                e.Handled = true; // 防止继续触发默认纵向滚动
            }
        }
    }
}