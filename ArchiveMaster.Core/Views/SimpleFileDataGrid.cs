using System.Collections;
using ArchiveMaster.Configs;
using ArchiveMaster.Converters;
using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using FzLib.Avalonia.Converters;
using SimpleFileInfo = ArchiveMaster.ViewModels.FileSystem.SimpleFileInfo;

namespace ArchiveMaster.Views;

public class SimpleFileDataGrid : DataGrid
{
    public static readonly StyledProperty<object> FooterProperty =
        AvaloniaProperty.Register<SimpleFileDataGrid, object>(
            nameof(Footer));

    public static readonly StyledProperty<bool> ShowCountProperty = AvaloniaProperty.Register<SimpleFileDataGrid, bool>(
        nameof(ShowCount), true);

    protected static readonly DateTimeConverter DateTimeConverter = new DateTimeConverter();

    protected static readonly FileLength2StringConverter FileLength2StringConverter = new FileLength2StringConverter();

    protected static readonly InverseBoolConverter InverseBoolConverter = new InverseBoolConverter();

    protected static readonly ProcessStatusColorConverter ProcessStatusColorConverter = new ProcessStatusColorConverter();

    public SimpleFileDataGrid()
    {
        AreRowDetailsFrozen = true;
        CanUserReorderColumns = true;
        CanUserResizeColumns = true;
        this[!IsReadOnlyProperty] =
            new Binding(nameof(TwoStepViewModelBase<TwoStepServiceBase<ConfigBase>, ConfigBase>.IsWorking));
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        RowDetailsVisibilityMode = SelectedItems.Count == 1 ? DataGridRowDetailsVisibilityMode.VisibleWhenSelected : DataGridRowDetailsVisibilityMode.Collapsed;
    }

    public virtual string ColumnIsCheckedHeader { get; init; } = "";

    public virtual double ColumnIsCheckedIndex { get; init; } = 0.1;

    public virtual string ColumnLengthHeader { get; init; } = "文件大小";

    public virtual double ColumnLengthIndex { get; init; } = 0.5;

    public virtual string ColumnMessageHeader { get; init; } = "信息";

    public virtual double ColumnMessageIndex { get; init; } = 999;

    public virtual string ColumnNameHeader { get; init; } = "文件名";

    public virtual double ColumnNameIndex { get; init; } = 0.3;

    public virtual string ColumnPathHeader { get; init; } = "路径";

    public virtual double ColumnPathIndex { get; init; } = 0.4;

    public virtual string ColumnStatusHeader { get; init; } = "状态";

    public virtual double ColumnStatusIndex { get; init; } = 0.2;

    public virtual string ColumnTimeHeader { get; init; } = "修改时间";

    public virtual double ColumnTimeIndex { get; init; } = 0.6;
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
            Header = ColumnIsCheckedHeader
        };
        var cellTemplate = new FuncDataTemplate<SimpleFileInfo>((value, namescope) =>
        {
            var rootPanel = this.GetLogicalAncestors().OfType<TwoStepPanelBase>().FirstOrDefault();

            return new CheckBox()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(SimpleFileInfo.IsChecked)),
                [!IsEnabledProperty] = new Binding("DataContext.IsWorking") //执行命令时，这CheckBox不可以Enable
                { Source = rootPanel, Converter = InverseBoolConverter },
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
            Binding = new Binding(nameof(SimpleFileInfo.Length))
            { Converter = FileLength2StringConverter, Mode = BindingMode.OneWay },
            IsReadOnly = true,
            MaxWidth = 120,
        };
    }

    protected virtual DataGridColumn GetMessageColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnMessageHeader,
            Binding = new Binding(nameof(SimpleFileInfo.Message)),
            IsReadOnly = true,
            Width = new DataGridLength(400),
        };
    }

    protected virtual DataGridColumn GetNameColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnNameHeader,
            Binding = new Binding(nameof(SimpleFileInfo.Name)),
            IsReadOnly = true,
            Width = new DataGridLength(400),
        };
    }

    protected virtual DataGridColumn GetPathColumn()
    {
        return new DataGridTextColumn()
        {
            Header = ColumnPathHeader,
            Binding = new Binding(nameof(SimpleFileInfo.RelativePath)),
            IsReadOnly = true,
            Width = new DataGridLength(400),
        };
    }

    protected virtual DataGridColumn GetProcessStatusColumn()
    {
        var column = new DataGridTemplateColumn
        {
            CanUserResize = false,
            CanUserReorder = false,
            CanUserSort = false,
            Header = ColumnStatusHeader,
            MaxWidth = 200,
        };

        var cellTemplate = new FuncDataTemplate<SimpleFileInfo>((value, namescope) => new Ellipse
        {
            Width = 8,
            Height = 8,
            [!Shape.FillProperty] = new Binding(nameof(SimpleFileInfo.Status))
            { Converter = ProcessStatusColorConverter }
        });

        column.CellTemplate = cellTemplate;
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
            var buttons = this.GetVisualDescendants()
                .OfType<Button>()
                .ToList();
            if (buttons.Count != 4)
            {
                return;
            }

            foreach (var btn in buttons)
            {
                btn[!IsEnabledProperty] =
                    new Binding(nameof(TwoStepViewModelBase<TwoStepServiceBase<ConfigBase>, ConfigBase>.IsWorking))
                    {
                        Converter = InverseBoolConverter
                    };
            }

            var tbtn = (ToggleButton)buttons[3];
            buttons[0].Click += (_, _) =>
            {
                foreach (SimpleFileInfo file in tbtn.IsChecked == true ? SelectedItems : ItemsSource)
                {
                    file.IsChecked = true;
                }
            };
            buttons[1].Click += (_, _) =>
            {
                foreach (SimpleFileInfo file in tbtn.IsChecked == true ? SelectedItems : ItemsSource)
                {
                    file.IsChecked = !file.IsChecked;
                }
            };
            buttons[2].Click += (_, _) =>
            {
                foreach (SimpleFileInfo file in tbtn.IsChecked == true ? SelectedItems : ItemsSource)
                {
                    file.IsChecked = false;
                }
            };
        }
        else
        {
            var stk = this
                .GetVisualDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault(p => p.Name == "stkSelectionButtons");
            if (stk != null)
            {
                ((Grid)stk.Parent).Children.Remove(stk);
            }
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
}