using System.Collections;
using System.Diagnostics;
using System.Globalization;
using ArchiveMaster.Configs;
using ArchiveMaster.Converters;
using ArchiveMaster.Helpers;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using ArchiveMaster.ViewModels.FileSystem;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentIcons.Avalonia;
using FluentIcons.Common;
using FzLib.Avalonia.Converters;
using FzLib.Avalonia.Dialogs;
using FzLib.IO;
using Serilog;
using SimpleFileInfo = ArchiveMaster.ViewModels.FileSystem.SimpleFileInfo;

namespace ArchiveMaster.Views;

public class SimpleFileDataGrid : DataGrid
{
    public static readonly StyledProperty<SimpleFileRowDetailItemCollection> AppendSimpleFileRowDetailItemsProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, SimpleFileRowDetailItemCollection>(
            nameof(AppendSimpleFileRowDetailItems));

    public static readonly StyledProperty<bool> DoubleTappedToOpenFileProperty =
        AvaloniaProperty.Register<TreeFileDataGrid, bool>(
            nameof(DoubleTappedToOpenFile), true);

    public static readonly StyledProperty<object> FooterProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, object>(
            nameof(Footer));

    public static readonly StyledProperty<bool> ShowCountProperty = AvaloniaProperty.Register<SimpleFileDataGrid, bool>(
        nameof(ShowCount), true);

    public static readonly StyledProperty<SimpleFileRowDetailItemCollection> SimpleFileRowDetailItemsProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, SimpleFileRowDetailItemCollection>(
            nameof(SimpleFileRowDetailItems), defaultValue:
            [
                new SimpleFileRowDetailItem("路径：", ".",
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

                        if (string.IsNullOrEmpty(f.Path))
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
                    })),

                new SimpleFileRowDetailItem("元数据：", ".",
                    new FuncValueConverter<SimpleFileInfo, InlineCollection>(f =>
                    {
                        InlineCollection ic = new InlineCollection();
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
                    })),

                new SimpleFileRowDetailItem("信息：", nameof(SimpleFileInfo.Message))
            ]);

    protected static readonly DateTimeConverter DateTimeConverter = new DateTimeConverter();

    protected static readonly FileDirLength2StringConverter FileDirLength2StringConverter =
        new FileDirLength2StringConverter();

    protected static readonly InverseBoolConverter InverseBoolConverter = new InverseBoolConverter();

    protected static readonly ProcessStatusColorConverter ProcessStatusColorConverter =
        new ProcessStatusColorConverter();

    protected static readonly FuncValueConverter<bool, double> BoolToOpacityConverter =
        new FuncValueConverter<bool, double>(b => b ? 1.0 : 0.5);

    public SimpleFileDataGrid()
    {
        AreRowDetailsFrozen = true;
        CanUserReorderColumns = true;
        CanUserResizeColumns = true;
        this[!IsReadOnlyProperty] = new Binding("IsWorking");
        DoubleTapped += SimpleFileDataGrid_DoubleTapped;
    }

    public SimpleFileRowDetailItemCollection AppendSimpleFileRowDetailItems
    {
        get => GetValue(AppendSimpleFileRowDetailItemsProperty);
        set => SetValue(AppendSimpleFileRowDetailItemsProperty, value);
    }

    public virtual string ColumnIsCheckedHeader { get; init; } = "";

    public virtual double ColumnIsCheckedIndex { get; init; } = 0.1;

    public virtual string ColumnLengthHeader { get; init; } = "文件大小";

    public virtual double ColumnLengthIndex { get; init; } = 0.5;

    public virtual double ColumnLengthMaxWidth { get; init; } = 120;

    public virtual string ColumnMessageHeader { get; init; } = "信息";

    public virtual double ColumnMessageIndex { get; init; } = 999;

    public virtual DataGridLength ColumnMessageWidth { get; init; } = new DataGridLength(400);

    public virtual string ColumnNameHeader { get; init; } = "文件名";

    public virtual DataGridLength ColumnNameWidth { get; init; } = new DataGridLength(400);

    public virtual double ColumnNameIndex { get; init; } = 0.3;

    public virtual string ColumnPathHeader { get; init; } = "路径";

    public virtual double ColumnPathIndex { get; init; } = 0.4;

    public virtual DataGridLength ColumnPathWidth { get; init; } = new DataGridLength(400);

    public virtual string ColumnStatusHeader { get; init; } = "状态";

    public virtual double ColumnStatusIndex { get; init; } = 0.2;

    public virtual string ColumnTimeHeader { get; init; } = "修改时间";

    public virtual double ColumnTimeIndex { get; init; } = 0.6;

    public bool DoubleTappedToOpenFile
    {
        get => GetValue(DoubleTappedToOpenFileProperty);
        set => SetValue(DoubleTappedToOpenFileProperty, value);
    }

    public object Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public bool ShowCount
    {
        get => GetValue(ShowCountProperty);
        set => SetValue(ShowCountProperty, value);
    }

    public SimpleFileRowDetailItemCollection SimpleFileRowDetailItems
    {
        get => GetValue(SimpleFileRowDetailItemsProperty);
        set => SetValue(SimpleFileRowDetailItemsProperty, value);
    }

    protected virtual (double Index, Func<DataGridColumn> Func)[] PresetColumns =>
    [
        (ColumnIsCheckedIndex, GetIsCheckedColumn),
        (ColumnStatusIndex, GetProcessStatusColumn),
        (ColumnNameIndex, GetNameColumn),
        (ColumnPathIndex, GetPathColumn),
        (ColumnLengthIndex, GetLengthColumn),
        (ColumnTimeIndex, GetTimeColumn),
        (ColumnMessageIndex, GetMessageColumn),
    ];

    protected override Type StyleKeyOverride => typeof(SimpleFileDataGrid);

    protected virtual DataGridColumn GetIsCheckedColumn()
    {
        var column = new DataGridTemplateColumn
        {
            CanUserResize = false,
            CanUserReorder = false,
            CanUserSort = false,
            Header = ColumnIsCheckedHeader,
            SortMemberPath = nameof(SimpleFileInfo.IsChecked)
        };
        var cellTemplate = new FuncDataTemplate<SimpleFileInfo>((value, namescope) =>
        {
            var rootPanel = this.GetLogicalAncestors().OfType<VerticalTwoStepPanelBase>().FirstOrDefault();
            return new ContentControl
            {
                Content = new CheckBox()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    [!ToggleButton.IsCheckedProperty] = new Binding(nameof(SimpleFileInfo.IsChecked)),
                    [!IsEnabledProperty] = new Binding("DataContext.IsWorking") //执行命令时，这CheckBox不可以Enable
                        { Source = rootPanel, Converter = InverseBoolConverter },
                },
                [!IsEnabledProperty] = new Binding(nameof(SimpleFileInfo.CanCheck)), //套两层控件，实现任一禁止选择则不允许选择
                [!OpacityProperty] = new Binding(nameof(SimpleFileInfo.CanCheck)) { Converter = BoolToOpacityConverter }
            };
        });

        column.CellTemplate = cellTemplate;
        return column;
    }

    protected virtual DataGridColumn GetLengthColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnLengthHeader,
            Binding = new Binding(".")
                { Converter = FileDirLength2StringConverter, Mode = BindingMode.OneWay },
            SortMemberPath = nameof(SimpleFileInfo.Length),
            IsReadOnly = true,
            MaxWidth = ColumnLengthMaxWidth,
            CellStyleClasses = { "Right" }
        };
    }

    protected virtual DataGridColumn GetMessageColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnMessageHeader,
            Binding = new Binding(nameof(SimpleFileInfo.Message)),
            IsReadOnly = true,
            Width = ColumnMessageWidth
        };
    }

    protected virtual DataGridColumn GetNameColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnNameHeader,
            Binding = new Binding(nameof(SimpleFileInfo.Name)),
            IsReadOnly = true,
            Width = ColumnNameWidth,
        };
    }

    protected virtual DataGridColumn GetPathColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnPathHeader,
            Binding = new Binding(nameof(SimpleFileInfo.RelativePath)),
            IsReadOnly = true,
            Width = ColumnPathWidth
        };
    }

    protected virtual DataGridColumn GetProcessStatusColumn()
    {
        var column = new DataGridLightColumn()
        {
            Header = ColumnStatusHeader,
            MaxWidth = 200,
            FillBinding = new Binding(nameof(SimpleFileInfo.Status)) { Converter = ProcessStatusColorConverter },
            SortMemberPath = nameof(SimpleFileInfo.Status)
        };

        return column;
    }

    protected virtual DataGridColumn GetTimeColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnTimeHeader,
            Binding = new Binding(nameof(SimpleFileInfo.Time))
            {
                Converter = DateTimeConverter,
                Mode = BindingMode.OneWay
            },
            IsReadOnly = true,
            CanUserResize = false
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (ColumnIsCheckedIndex >= 0)
        {
            var btnSelectAll = e.NameScope.Find<Button>("PART_SelectAllButton");
            var btnSwitchSelection = e.NameScope.Find<Button>("PART_SwitchSelectionButton");
            var btnSelectNone = e.NameScope.Find<Button>("PART_SelectNoneButton");
            var btnProcessCheckedOnly = e.NameScope.Find<ToggleButton>("PART_ProcessCheckedOnlyButton");
            var btnSearch = e.NameScope.Find<Button>("PART_SearchButton");
            var btnFilter = e.NameScope.Find<Button>("PART_FilterButton");


            foreach (var btn in new Button[]
                         {
                             btnSelectAll, btnSwitchSelection, btnSelectNone, btnProcessCheckedOnly, btnSearch,
                             btnFilter
                         }
                         .Where(p => p != null))
            {
                btn[!IsEnabledProperty] =
                    new Binding("IsWorking")
                    {
                        Converter = InverseBoolConverter
                    };
            }

            IEnumerable<SimpleFileInfo> GetItems()
            {
                return (btnProcessCheckedOnly.IsChecked == true ? SelectedItems : ItemsSource)?
                    .OfType<SimpleFileInfo>() ?? [];
            }

            //全选
            if (btnSelectAll != null)
            {
                btnSelectAll.Click += (_, _) =>
                {
                    foreach (var file in GetItems().Where(p => p.CanCheck))
                    {
                        file.IsChecked = true;
                    }
                };
            }

            //全不选
            if (btnSelectNone != null)
            {
                btnSelectNone.Click += (_, _) =>
                {
                    foreach (var file in GetItems().Where(p => p.CanCheck))
                    {
                        file.IsChecked = false;
                    }
                };
            }

            //反选
            if (btnSwitchSelection != null)
            {
                btnSwitchSelection.Click += (_, _) =>
                {
                    foreach (var file in GetItems().Where(p => p.CanCheck))
                    {
                        file.IsChecked = !file.IsChecked;
                    }
                };
            }

            //搜索
            if (btnSearch != null)
            {
                var searchGrid = (btnSearch.Flyout as Flyout)?.Content as Grid;
                Debug.Assert(searchGrid != null);
                var searchTextBox = searchGrid.Children[0] as TextBox;
                Debug.Assert(searchTextBox != null);
                var searchButton = searchGrid.Children[1] as Button;
                Debug.Assert(searchButton != null);
                searchButton.Click += (_, _) => Search();
                searchTextBox.KeyDown += (_, e2) =>
                {
                    if (e2.Key is Key.Enter or Key.Return)
                    {
                        Search();
                    }
                };

                async void Search()
                {
                    var text = searchTextBox.Text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return;
                    }

                    var helper = new FileFilterHelper(new FileFilterRule
                    {
                        IncludeFiles = $"*{text}*"
                    });
                    SelectedItems.Clear();
                    foreach (var file in ItemsSource.OfType<SimpleFileInfo>().Where(p => helper.IsMatched(p)))
                    {
                        SelectedItems.Add(file);
                    }

                    if (SelectedItems.Count > 0)
                    {
                        ScrollIntoView(SelectedItems[0], Columns[0]);
                        await HostServices.GetRequiredService<IDialogService>()
                            .ShowOkDialogAsync("搜索", $"搜索到{SelectedItems.Count}条记录，已全部选中");
                    }
                    else
                    {
                        await HostServices.GetRequiredService<IDialogService>()
                            .ShowWarningDialogAsync("搜索", $"没有搜索到任何记录");
                    }
                }
            }

            //筛选
            if (btnFilter != null)
            {
                var filterGrid = (btnFilter.Flyout as Flyout)?.Content as Grid;
                Debug.Assert(filterGrid != null);
                var filterPanel = filterGrid.Children[0] as FileFilterPanel;
                Debug.Assert(filterPanel != null);
                filterPanel.Filter = new FileFilterRule();
                var filterButton = filterGrid.Children[1] as Button;
                Debug.Assert(filterButton != null);
                filterButton.Click += (_, _) => Filter();

                async void Filter()
                {
                    var helper = new FileFilterHelper(filterPanel.Filter);
                    int count = 0;
                    foreach (var file in ItemsSource.OfType<SimpleFileInfo>())
                    {
                        if (helper.IsMatched(file))
                        {
                            file.IsChecked = true;
                            count++;
                        }
                        else
                        {
                            file.IsChecked = false;
                        }
                    }

                    if (count > 0)
                    {
                        await HostServices.GetRequiredService<IDialogService>()
                            .ShowOkDialogAsync("筛选", $"筛选到{count}条记录，已全部勾选");
                    }
                    else
                    {
                        await HostServices.GetRequiredService<IDialogService>()
                            .ShowWarningDialogAsync("筛选", $"没有筛选到任何记录");
                    }
                }
            }
        }
        else
        {
            //删除占位
            var stk = e.NameScope.Find<StackPanel>("PART_DataGridButtons");
            if (stk != null)
            {
                ((Grid)stk.Parent)?.Children.Remove(stk);
            }
        }

        if (SimpleFileRowDetailItems != null && AppendSimpleFileRowDetailItems is { Count: > 0 })
        {
            SimpleFileRowDetailItems.InsertRange(SimpleFileRowDetailItems.Count - 1, AppendSimpleFileRowDetailItems);
        }
    }

    protected virtual void OnFileDoubleTapped(SimpleFileInfo file)
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

    protected override void OnInitialized()
    {
        base.OnInitialized();

        int columnCount = Columns.Count;
        //插入的，从后往前插，这样不会打乱顺序
        var ordered1 = PresetColumns
            .Where(p => p.Index >= 0)
            .Where(p => p.Index < columnCount)
            .OrderByDescending(p => p.Index);

        //追加的，按序号从小到大调用Add方法
        var ordered2 = PresetColumns
            .Where(p => p.Index >= 0)
            .Where(p => p.Index >= columnCount)
            .OrderBy(p => p.Index);

        foreach (var column in ordered1)
        {
            Columns.Insert((int)column.Index, column.Func());
        }

        foreach (var column in ordered2)
        {
            Columns.Add(column.Func());
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        RowDetailsVisibilityMode = SelectedItems.Count == 1
            ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected
            : DataGridRowDetailsVisibilityMode.Collapsed;
    }

    private void SimpleFileDataGrid_DoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Source is Visual { DataContext: SimpleFileInfo file })
        {
            OnFileDoubleTapped(file);
        }
    }
}