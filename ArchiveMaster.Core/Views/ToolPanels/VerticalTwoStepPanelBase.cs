using Avalonia.Controls;
using System;
using ArchiveMaster.ViewModels;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ArchiveMaster.Views
{
    public class VerticalTwoStepPanelBase : TwoStepPanelBase
    {
        protected override Type StyleKeyOverride { get; } = typeof(VerticalTwoStepPanelBase);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            gridSplitter.DragStarted += (_, _) =>
            {
                //开始拖动后，解除相关限制。
                //1.取消滚动区高度限制
                //2.配置单元格实际高度为当前高度，避免闪现
                //3.配置单元格最大高度为需要的高度
                configScrViewer.MaxHeight = double.MaxValue;
                container.RowDefinitions[0].Height =
                    new GridLength(container.RowDefinitions[0].ActualHeight, GridUnitType.Pixel);
                container.RowDefinitions[0].MaxHeight = configScrViewer.Extent.Height + 16;
            };
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ResetSize(e.HeightChanged, e.WidthChanged);
        }

        private void ResetSize(bool resetHeight = true, bool resetWidth = true)
        {
            if (resetHeight)
            {
                configScrViewer.MaxHeight = Bounds.Height > 700 ? 300d : 200d;
                container.RowDefinitions[0].Height = GridLength.Auto;
                container.RowDefinitions[0].MaxHeight = double.MaxValue;
            }

            if (resetWidth)
            {
                bottomTwoLine.IsVisible = !(bottomOneLine.IsVisible = Bounds.Width > 500);
            }
        }
    }
}