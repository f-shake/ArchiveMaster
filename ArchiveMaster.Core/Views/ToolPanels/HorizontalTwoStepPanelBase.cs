using Avalonia;
using Avalonia.Controls;

namespace ArchiveMaster.Views;

public class HorizontalTwoStepPanelBase : TwoStepPanelBase
{
        
    protected override Type StyleKeyOverride { get; } = typeof(HorizontalTwoStepPanelBase);

    public static readonly StyledProperty<GridLength> LeftPanelWidthProperty = AvaloniaProperty.Register<HorizontalTwoStepPanelBase, GridLength>(
        nameof(LeftPanelWidth),GridLength.Star);

    public GridLength LeftPanelWidth
    {
        get => GetValue(LeftPanelWidthProperty);
        set => SetValue(LeftPanelWidthProperty, value);
    }

    public static readonly StyledProperty<GridLength> RightPanelWidthProperty = AvaloniaProperty.Register<HorizontalTwoStepPanelBase, GridLength>(
        nameof(RightPanelWidth),GridLength.Star);

    public GridLength RightPanelWidth
    {
        get => GetValue(RightPanelWidthProperty);
        set => SetValue(RightPanelWidthProperty, value);
    }
   
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (container.ColumnDefinitions[0].Width != LeftPanelWidth)
        {
            container.ColumnDefinitions[0].Width = LeftPanelWidth;
        }

        if (container.ColumnDefinitions[2].Width != RightPanelWidth)
        {
            container.ColumnDefinitions[2].Width = RightPanelWidth;
        }
        ResetSize(e.HeightChanged, e.WidthChanged);
    }

    private void ResetSize(bool resetHeight = true, bool resetWidth = true)
    {
        // if (resetHeight)
        // {
        //     configScrViewer.MaxHeight=double.MaxValue;// = Bounds.Height > 700 ? 300d : 200d;
        //     container.RowDefinitions[0].Height = GridLength.Auto;
        //     container.RowDefinitions[0].MaxHeight = double.MaxValue;
        // }

        if (resetWidth)
        {
            bottomTwoLine.IsVisible = !(bottomOneLine.IsVisible = Bounds.Width > 500);
        }
    }
}