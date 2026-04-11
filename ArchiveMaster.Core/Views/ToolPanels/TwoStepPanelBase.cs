using ArchiveMaster.Services;
using ArchiveMaster.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace ArchiveMaster.Views;

public abstract class TwoStepPanelBase : PanelBase
{
    public static readonly StyledProperty<object> ConfigsContentProperty =
        AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(ConfigsContent));

    public static readonly StyledProperty<object> ExecuteButtonContentProperty
        = AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(ExecuteButtonContent), "执行");

    public static readonly StyledProperty<object> InitializeButtonContentProperty
        = AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(InitializeButtonContent), "初始化");

    public static readonly StyledProperty<object> ResetButtonContentProperty
        = AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(ResetButtonContent), "重置");

    public static readonly StyledProperty<object> ResultsContentProperty =
        AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(ResultsContent));

    public static readonly StyledProperty<bool> ShowBottomBarProperty =
        AvaloniaProperty.Register<VerticalTwoStepPanelBase, bool>(
            nameof(ShowBottomBar), true);

    public static readonly StyledProperty<object> StopButtonContentProperty
            = AvaloniaProperty.Register<VerticalTwoStepPanelBase, object>(nameof(StopButtonContent), "取消");
    protected Grid bottomOneLine;

    protected Grid bottomTwoLine;

    protected ScrollViewer configScrViewer;

    protected Grid container;

    protected GridSplitter gridSplitter;

    protected TextBlock messageTextBlock;

    public object ConfigsContent
    {
        get => GetValue(ConfigsContentProperty);
        set => SetValue(ConfigsContentProperty, value);
    }

    public object ExecuteButtonContent
    {
        get => GetValue(ExecuteButtonContentProperty);
        set => SetValue(ExecuteButtonContentProperty, value);
    }

    public object InitializeButtonContent
    {
        get => GetValue(InitializeButtonContentProperty);
        set => SetValue(InitializeButtonContentProperty, value);
    }

    public object ResetButtonContent
    {
        get => GetValue(ResetButtonContentProperty);
        set => SetValue(ResetButtonContentProperty, value);
    }

    public object ResultsContent
    {
        get => GetValue(ResultsContentProperty);
        set => SetValue(ResultsContentProperty, value);
    }

    public bool ShowBottomBar
    {
        get => GetValue(ShowBottomBarProperty);
        set => SetValue(ShowBottomBarProperty, value);
    }
    public object StopButtonContent
    {
        get => GetValue(StopButtonContentProperty);
        set => SetValue(StopButtonContentProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        gridSplitter = e.NameScope.Find<GridSplitter>("PART_GridSplitter");
        configScrViewer = e.NameScope.Find<ScrollViewer>("PART_Configs");
        bottomOneLine = e.NameScope.Find<Grid>("PART_BottomOneLine");
        bottomTwoLine = e.NameScope.Find<Grid>("PART_BottomTwoLine");
        container = e.NameScope.Find<Grid>("PART_ContentContainer");
        messageTextBlock = e.NameScope.Find<TextBlock>("PART_MessageOneLine") ??
                           e.NameScope.Find<TextBlock>("PART_MessageTwoLine");
        if (gridSplitter == null || configScrViewer == null || bottomOneLine == null || bottomTwoLine == null)
        {
            throw new InvalidOperationException($"{nameof(TwoStepPanelBase)}的模板不对应");
        }

        if (messageTextBlock != null)
        {
            messageTextBlock.Tapped += (s, e) =>
            {
                if (DataContext is ITwoStepViewModel { TwoStepService.CurrentProcessingFile: not null } vm)
                {
                    vm.SelectedFile = vm.TwoStepService.CurrentProcessingFile;
                }
            };
            messageTextBlock.PointerEntered += (s, e) =>
            {
                if (DataContext is ITwoStepViewModel { TwoStepService.CurrentProcessingFile: not null } vm)
                {
                    messageTextBlock.Cursor = new Cursor(StandardCursorType.Hand);
                }
                else
                {
                    messageTextBlock.Cursor = new Cursor(StandardCursorType.Arrow);
                }
            };
        }
    }
}