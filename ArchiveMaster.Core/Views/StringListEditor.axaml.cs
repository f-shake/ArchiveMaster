using System.Collections;
using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FzLib.Avalonia.Dialogs.Pickers;

namespace ArchiveMaster.Views
{
    public partial class StringListEditor : UserControl
    {
        public static readonly StyledProperty<ObservableStringList> ItemsSourceProperty =
            AvaloniaProperty.Register<StringListEditor, ObservableStringList>(
                nameof(ItemsSource));

        public StringListEditor()
        {
            InitializeComponent();
        }

        public ObservableStringList ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var list = GetItemsSourceList();
            list.Add("新项目");
            scr.Offset = new Vector(int.MaxValue, 0); //滚动到最右侧
            FocusTextBox(list.Count - 1);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var list = GetItemsSourceList();
            list.Clear();
        }

        private void FocusTextBox(int index)
        {
            //将光标移动到新插入的项上
            if (items.ContainerFromIndex(index) is not ContentPresenter container)
            {
                throw new ArgumentNullException(nameof(container));
            }

            container.Loaded += (s, _) =>
            {
                var txt = (s as ContentPresenter ?? throw new InvalidOperationException())
                    .GetVisualDescendants()
                    .OfType<TextBox>()
                    .First();
                txt.Focus();
                txt.SelectAll();
            };
        }

        private ObservableStringList GetItemsSourceList()
        {
            if (ItemsSource is null)
            {
                throw new InvalidOperationException("ItemsSource为空");
            }

            return ItemsSource;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var editableString = button.DataContext as EditableString;
            if (editableString == null)
            {
                throw new InvalidOperationException("DataContext为空");
            }

            var list = GetItemsSourceList();
            list.Remove(editableString);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            //获取到当前TextBox的DataContext
            var textBox = sender as TextBox;
            if (textBox?.DataContext is not EditableString editableString)
            {
                throw new InvalidOperationException("DataContext为空");
            }

            int index = GetItemsSourceList().IndexOf(editableString);
            if (index == -1)
            {
                throw new InvalidOperationException("DataContext不在ItemsSource中");
            }

            //右侧插入一个空项
            var newText = new EditableString("");
            GetItemsSourceList().Insert(index + 1, newText);

            //如果光标在中间，则将光标前的字符串和光标后的字符串拆开
            var text = textBox.Text ?? "";
            var caretIndex = textBox.CaretIndex;
            if (caretIndex >= 0 && caretIndex < text.Length)
            {
                var text1 = text[..caretIndex];
                var text2 = text[caretIndex..];
                editableString.Value = text1;
                newText.Value = text2;
            }

            FocusTextBox(index + 1);
        }
    }
}