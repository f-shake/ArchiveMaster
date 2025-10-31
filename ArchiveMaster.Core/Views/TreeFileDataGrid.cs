using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using ArchiveMaster.Configs;
using ArchiveMaster.Converters;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using FzLib;
using FzLib.Avalonia.Converters;
using Serilog;
using SimpleFileInfo = ArchiveMaster.ViewModels.FileSystem.SimpleFileInfo;
using TreeFileDirInfo = ArchiveMaster.ViewModels.FileSystem.TreeFileDirInfo;

namespace ArchiveMaster.Views;

public class TreeFileDataGrid : SimpleFileDataGrid
{
    public static readonly StyledProperty<bool> IsDirCheckBoxVisibleProperty =
        AvaloniaProperty.Register<TreeFileDataGrid, bool>(
            nameof(IsDirCheckBoxVisible), false);

    public static readonly StyledProperty<bool> IsFileCheckBoxVisibleProperty =
        AvaloniaProperty.Register<TreeFileDataGrid, bool>(
            nameof(IsFileCheckBoxVisible), true);

    public static readonly StyledProperty<int> RootDepthProperty
        = AvaloniaProperty.Register<TreeFileDataGrid, int>(nameof(RootDepth), 1);

    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<TreeFileDataGrid, string>(
            nameof(SearchText));

    protected static readonly TreeFileCheckBoxVisibleConverter TreeFileCheckBoxVisibleConverter =
        new TreeFileCheckBoxVisibleConverter();

    protected static readonly TreeFileDirLengthConverter TreeFileDirLengthConverter = new TreeFileDirLengthConverter();

    public TreeFileDataGrid()
    {
        DoubleTapped += DataGridDoubleTapped;
    }

    public override double ColumnPathIndex => -1;

    public bool IsDirCheckBoxVisible
    {
        get => GetValue(IsDirCheckBoxVisibleProperty);
        set => SetValue(IsDirCheckBoxVisibleProperty, value);
    }

    public bool IsFileCheckBoxVisible
    {
        get => GetValue(IsFileCheckBoxVisibleProperty);
        set => SetValue(IsFileCheckBoxVisibleProperty, value);
    }

    public int RootDepth
    {
        get => GetValue(RootDepthProperty);
        set => SetValue(RootDepthProperty, value);
    }

    public string SearchText
    {
        get => GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string TreeFileDirLengthFormat { get; set; } = Converters.TreeFileDirLengthConverter.DefaultFormat;

    protected override Type StyleKeyOverride => typeof(TreeFileDataGrid);

    public void ExpandTo(TreeFileDirInfo file)
    {
        Stack<TreeDirInfo> stack = new Stack<TreeDirInfo>();
        if (file.Parent == null || file.Parent.IsExpanded)
        {
            return;
        }

        stack.Push(file.Parent);
        while (stack.Peek().Parent != null && stack.Peek().Parent.IsExpanded == false)
        {
            stack.Push(stack.Peek().Parent);
        }

        while (stack.Count > 0)
        {
            Expand(stack.Pop());
        }
    }

    public void Search()
    {
        if (ItemsSource is not AvaloniaList<SimpleFileInfo> items)
        {
            throw new Exception($"{nameof(ItemsSource)}必须为{nameof(AvaloniaList<SimpleFileInfo>)}");
        }

        if (items.Count == 0 || items[0] is not TreeDirInfo root)
        {
            throw new ArgumentException($"{nameof(ItemsSource)}的根节点必须为单个{nameof(TreeDirInfo)}");
        }

        Collapse(root);
        if (items.Count > 1)
        {
            throw new ArgumentException($"{nameof(ItemsSource)}的根节点必须为单个{nameof(TreeDirInfo)}");
        }

        try
        {
            var files = root.Search(SearchText);

            SelectedItems.Clear();
            foreach (var file in files)
            {
                ExpandTo(file);
                // file.IsChecked = true;
                SelectedItems.Add(file);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "搜索文件失败");
            Debug.Assert(false);
        }
    }

    protected override DataGridColumn GetIsCheckedColumn()
    {
        var column = new DataGridTemplateColumn
        {
            CanUserResize = false,
            CanUserReorder = false,
            CanUserSort = false,
            Header = ColumnIsCheckedHeader
        };
        var cellTemplate = new FuncDataTemplate<SimpleFileInfo>((value, namescope) =>
        {
            var rootPanel = this.GetLogicalAncestors().OfType<TwoStepPanelBase>().FirstOrDefault();

            var chk = new CheckBox()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(TreeFileDirInfo.IsTreeItemChecked)),
                IsThreeState = true,
                [!IsVisibleProperty] = new Binding(".")
                {
                    Converter = TreeFileCheckBoxVisibleConverter,
                    ConverterParameter = this,
                },
                [!IsEnabledProperty] = new Binding("DataContext.IsWorking") //执行命令时，这CheckBox不可以Enable
                    { Source = rootPanel, Converter = InverseBoolConverter },
            };
            //在CheckBox上双击，不被识别为展开或折叠
            chk.DoubleTapped += (s, e) => e.Handled = true;
            return chk;
        });

        column.CellTemplate = cellTemplate;
        return column;
    }

    protected override DataGridColumn GetLengthColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnLengthHeader,
            Binding = new Binding()
            {
                Converter = TreeFileDirLengthConverter,
                ConverterParameter = TreeFileDirLengthFormat,
                Mode = BindingMode.OneWay
            },
            IsReadOnly = true,
        };
    }

    protected override DataGridColumn GetNameColumn()
    {
        var column = new DataGridTemplateColumn
        {
            Header = ColumnNameHeader
        };
        var cellTemplate = new FuncDataTemplate<TreeFileDirInfo>((value, namescope) =>
        {
            int halfWidth = 16;
            int fullWidth = halfWidth * 2;
            int halfHeight = 16;
            int height = halfHeight * 2;
            var lines = new Canvas()
            {
                Width = fullWidth * (value.Depth - RootDepth),
                Height = height,
            };

            List<(double x1, double y1, double x2, double y2)> linePoints =
                new List<(double x1, double y1, double x2, double y2)>();

            if (value.Depth > RootDepth) //根目录不需要
            {
                //首先处理最右侧
                int i = value.Depth - RootDepth;

                if (value.IsLast()) //如果是最后一个，那么是半高的竖线和半宽的横线
                {
                    linePoints.Add((fullWidth * i - halfWidth, 0, fullWidth * i - halfWidth, halfHeight));
                }
                else //否则，是全高的竖线和半宽的横线
                {
                    linePoints.Add((fullWidth * i - halfWidth, 0, fullWidth * i - halfWidth, height));
                }

                linePoints.Add((fullWidth * i - halfWidth, halfHeight, fullWidth * i, halfHeight));

                var parent = value.Parent; //取当前项的父级
                //从右往左
                for (i = value.Depth - 1 - RootDepth; i > 0; i--)
                {
                    //如果父级不是最后一个，则在父级对应的水平位置绘制竖线
                    if (!parent.IsLast())
                    {
                        linePoints.Add((fullWidth * i - halfWidth, 0, fullWidth * i - halfWidth, height));
                    }

                    parent = parent.Parent;
                }

                //最后根据点集绘制所有线
                foreach (var point in linePoints)
                {
                    lines.Children.Add(new Line()
                    {
                        StartPoint = new Point(point.x1, point.y1),
                        EndPoint = new Point(point.x2, point.y2),
                        [!Shape.StrokeProperty] = new DynamicResourceExtension("Foreground0"),
                    });
                }
            }

            //文件文件夹图标
            var icon = new ContentPresenter()
            {
                Content = value is TreeDirInfo
                    ? new FluentIcon { Icon = Icon.Folder }
                    : new FluentIcon { Icon = Icon.Document },
                Foreground = value is TreeDirInfo ? Brushes.ForestGreen : Brushes.Coral
            };

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children = { lines, icon, GetNameContentTemplate() }
            };
        });

        column.CellTemplate = cellTemplate;
        return column;
    }

    protected virtual Control GetNameContentTemplate()
    {
        var tbkName = new TextBlock()
        {
            [!TextBlock.TextProperty] = new Binding(nameof(SimpleFileInfo.Name)),
        };
        return tbkName;
    }

    protected override void OnFileDoubleTapped(SimpleFileInfo file)
    {
    }

    private void Collapse(TreeDirInfo dir)
    {
        if (dir.IsExpanded == false)
        {
            return;
        }

        if (ItemsSource is not AvaloniaList<SimpleFileInfo> items)
        {
            throw new Exception($"{nameof(ItemsSource)}必须为{nameof(AvaloniaList<SimpleFileInfo>)}");
        }

        foreach (var subDir in dir.SubDirs.Where(p => p.IsExpanded))
        {
            Collapse(subDir);
        }

        items.RemoveRange(items.IndexOf(dir) + 1, dir.Subs.Count);
        dir.IsExpanded = false;
    }

    private void DataGridDoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Source is Visual { DataContext: TreeDirInfo dir })
        {
            if (ItemsSource is not AvaloniaList<SimpleFileInfo> items)
            {
                return;
            }

            if (!dir.IsExpanded)
            {
                Expand(dir);
            }
            else
            {
                Collapse(dir);
            }
        }
        else
        {
            if (!DoubleTappedToOpenFile)
            {
                return;
            }

            if (e.Source is Visual { DataContext: TreeFileInfo file })
            {
                try
                {
                    Process.Start(new ProcessStartInfo(file.Path)
                    {
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "打开文件失败");
                }
            }
        }
    }

    private void Expand(TreeDirInfo dir)
    {
        if (dir.IsExpanded == true)
        {
            return;
        }

        if (ItemsSource is not AvaloniaList<SimpleFileInfo> items)
        {
            throw new Exception($"{nameof(ItemsSource)}必须为{nameof(AvaloniaList<SimpleFileInfo>)}");
        }

        // dir.Subs.ForEach(p => p.IsChecked = false);
        items.InsertRange(items.IndexOf(dir) + 1, dir.Subs);
        dir.IsExpanded = true;
    }
}