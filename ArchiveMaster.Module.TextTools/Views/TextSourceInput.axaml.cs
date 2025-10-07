using ArchiveMaster.Helpers;
using Avalonia.Controls;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FzLib.Avalonia.Dialogs.Pickers;

namespace ArchiveMaster.Views
{
    public partial class TextSourceInput : UserControl
    {
        public static readonly StyledProperty<TextSource> SourceProperty =
            AvaloniaProperty.Register<TextSourceInput, TextSource>(
                nameof(Source));

        public TextSourceInput()
        {
            InitializeComponent();
        }
        public TextSource Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
            {
                return;
            }

            var pickerOptions = FilePickerOptionsBuilder.Create()
                .AddFilter("所有文本文档", "txt", "doc", "docx", "md", "txt")
                .AddFilter("Word文档", "doc", "docx")
                .AddFilter("Markdown文档", "md")
                .AddFilter("纯文本", "txt")
                .AddFilter("所有文件（作为纯文本读取）", "*")
                .AllowMultiple()
                .BuildOpenOptions();
            var files = await storageProvider.OpenFilePickerAsync(pickerOptions);
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    Source.Files.Add(new DocFile(file.GetPath()));
                }
            }
        }

        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            Source.Files.Clear();
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            var text = await (topLevel.Clipboard?.GetTextAsync() ?? Task.FromResult(""));
            if (!string.IsNullOrWhiteSpace(text))
            {
                Source.Text = text;
            }
        }
        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is DocFile file)
            {
                Source.Files.Remove(file);
            }
        }
    }
}