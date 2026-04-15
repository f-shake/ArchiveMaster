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
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    public static readonly StyledProperty<bool> DoubleTappedToOpenFileProperty =
        AvaloniaProperty.Register<TreeFileDataGrid, bool>(
            nameof(DoubleTappedToOpenFile), true);

    public static readonly StyledProperty<object> FooterProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, object>(
            nameof(Footer));

    public static readonly StyledProperty<IDataTemplate> RowDetailsPopupTemplateProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, IDataTemplate>(
            nameof(RowDetailsPopupTemplate));

    public static readonly StyledProperty<bool> ShowCountProperty = AvaloniaProperty.Register<SimpleFileDataGrid, bool>(
            nameof(ShowCount), true);
    protected static readonly FuncValueConverter<bool, double> BoolToOpacityConverter =
        new FuncValueConverter<bool, double>(b => b ? 1.0 : 0.5);

    protected static readonly DateTimeConverter DateTimeConverter = new DateTimeConverter();

    protected static readonly FileDirLength2StringConverter FileDirLength2StringConverter =
        new FileDirLength2StringConverter();

    protected static readonly InverseBoolConverter InverseBoolConverter = new InverseBoolConverter();

    protected static readonly ProcessStatusColorConverter ProcessStatusColorConverter =
        new ProcessStatusColorConverter();

    private Popup detailPopup;

    public SimpleFileDataGrid()
    {
        AreRowDetailsFrozen = true;
        CanUserReorderColumns = true;
        CanUserResizeColumns = true;
        this[!IsReadOnlyProperty] = new Binding("IsWorking");
        DoubleTapped += SimpleFileDataGrid_DoubleTapped;
        SetAutoScrollToSelectedItem();
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

    public virtual double ColumnNameIndex { get; init; } = 0.3;

    public virtual DataGridLength ColumnNameWidth { get; init; } = new DataGridLength(400);

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

    public IDataTemplate RowDetailsPopupTemplate
    {
        get => GetValue(RowDetailsPopupTemplateProperty);
        set => SetValue(RowDetailsPopupTemplateProperty, value);
    }
    public bool ShowCount
    {
        get => GetValue(ShowCountProperty);
        set => SetValue(ShowCountProperty, value);
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
        
        detailPopup = e.NameScope.Find<Popup>("PART_Popup");
        
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

    protected override void OnLoadingRow(DataGridRowEventArgs e)
    {
        base.OnLoadingRow(e);
        e.Row.GetObservable(DataGridRow.IsSelectedProperty).Subscribe(p =>
        {
            if (detailPopup == null)
            {
                return;
            }
            if (p)
            {
                detailPopup.PlacementTarget = e.Row;
                detailPopup.IsOpen = true;
                Focus();
            }
        });
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (detailPopup == null)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.IsFocused == true || IsFocused || detailPopup.IsFocused)
        {
            Debug.WriteLine("不关闭Popup（窗口、DataGrid或Popup被Focused）");
            return;
        }

        var f = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if (f is Control c && c.GetVisualAncestors().Any(p => p.Name == "PART_PopupBorder"))
        {
            Debug.WriteLine("不关闭Popup（内部控件被Focused）");
            return;
        }

        if (detailPopup.IsPointerOverPopup || detailPopup.IsPointerOver)
        {
            Debug.WriteLine("不关闭Popup（鼠标在Popup上）");
        }

        Debug.WriteLine($"当前Focus：{f}");

        Debug.WriteLine("关闭Popup");
        detailPopup.IsOpen = false;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        detailPopup?.MaxWidth = e.NewSize.Width;
    }
    private void SetAutoScrollToSelectedItem()
    {
        this.GetObservable(SelectedItemProperty).Subscribe(item =>
        {
            if (item != null)
            {
                ScrollIntoView(item, null);
            }
        });
    }

    private void SimpleFileDataGrid_DoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Source is Visual { DataContext: SimpleFileInfo file })
        {
            // OnFileDoubleTapped(file);
        }
    }
}