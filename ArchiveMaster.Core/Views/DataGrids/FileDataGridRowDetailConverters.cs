using System.Globalization;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using FluentIcons.Avalonia;
using FluentIcons.Common;

namespace ArchiveMaster.Views;

internal static class FileDataGridRowDetailConverters
{
    public static FuncValueConverter<SimpleFileInfo, InlineCollection> FilePathToInlineConverter =
        new FuncValueConverter<SimpleFileInfo, InlineCollection>(f =>
        {
            InlineCollection ic = new InlineCollection();

            void AddOpenFileButton()
            {
                ic.Add(new InlineUIContainer(new Button()
                {
                    Content = new FluentIcon { Icon = Icon.FolderOpen, Width = 20 },
                    Padding = new Thickness(),
                    Height = 16,
                    Classes = { "Link" },
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(8, -2),
                    Flyout = new MenuFlyout
                    {
                        Items =
                        {
                            new MenuItem
                            {
                                Header = "打开文件",
                                Command = GlobalCommands.Instance.OpenFileCommand,
                                CommandParameter = f.Path,
                            },
                            new MenuItem
                            {
                                Header = "打开目录",
                                Command = GlobalCommands.Instance.OpenParentDirCommand,
                                CommandParameter = f.Path,
                            }
                        }
                    }
                }));
            }

            if (f == null || string.IsNullOrEmpty(f.Path))
            {
                ic.Add(new Run("（空）"));
                return ic;
            }

            //无相对路径，显示绝对路径
            if (string.IsNullOrEmpty(f.RelativePath))
            {
                ic.Add(new Run(f.Path));
                AddOpenFileButton();
                return ic;
            }

            //相对路径和绝对路径不一致，均显示
            if (!f.Path.EndsWith(f.RelativePath))
            {
                ic.Add(new Run($"{f.Path}（{f.RelativePath}）"));
                AddOpenFileButton();
                return ic;
            }

            //普通情况，显示绝对路径，并且标识相对路径
            var relPath = f.Path[..^f.RelativePath.Length];
            ic.Add(new Run(relPath));
            ic.Add(new Run(f.RelativePath) { TextDecorations = TextDecorations.Underline });
            AddOpenFileButton();
            return ic;
        });

    public static FuncValueConverter<SimpleFileInfo, InlineCollection> FileMetadataToInlineConverter =
        new FuncValueConverter<SimpleFileInfo, InlineCollection>(f =>
        {
            InlineCollection ic = new InlineCollection();
            if (f == null)
            {
                return null;
            }
            if (!f.IsDir)
            {
                ic.Add(new Run((string)Converters.Converters.FileLength.Convert(f.Length,
                    typeof(string), null,
                    CultureInfo.CurrentCulture))
                {
                    FontStyle = FontStyle.Italic
                });
            }

            ic.Add("    ");
            ic.Add(new Run(f.Time.ToString("yyyy-MM-dd HH:mm:ss")));
            return ic;
        });

}