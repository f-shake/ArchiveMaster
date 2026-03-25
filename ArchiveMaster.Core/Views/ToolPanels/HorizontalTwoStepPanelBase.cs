using Avalonia.Controls;

namespace ArchiveMaster.Views;

public class HorizontalTwoStepPanelBase : TwoStepPanelBase
{
        
    protected override Type StyleKeyOverride { get; } = typeof(HorizontalTwoStepPanelBase);
        
   
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
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