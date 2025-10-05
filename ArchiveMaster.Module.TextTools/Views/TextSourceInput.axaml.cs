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
        public TextSourceInput()
        {
            InitializeComponent();
        }

        public static readonly StyledProperty<TextSource> SourceProperty =
            AvaloniaProperty.Register<TextSourceInput, TextSource>(
                nameof(Source));

        public TextSource Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            var text = await (topLevel.Clipboard?.GetTextAsync()??Task.FromResult(""));
            if (!string.IsNullOrWhiteSpace(text))
            {
                Source.Text= text;
            }
        }

        private async void AddFilesButton_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider == null)
            {
                return;
            }

            var pickerOptions = FilePickerOptionsBuilder.Create()
                .AddFilter("文本文件", "txt", "doc", "docx")
                .AddFilter("Word文档", "doc", "docx")
                .AddFilter("纯文本", "txt")
                .AddFilter("所有文件", "*")
                .AllowMultiple()
                .BuildOpenOptions();
            var files =await storageProvider.OpenFilePickerAsync(pickerOptions);
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