using Avalonia;
using Avalonia.Controls;
using System;
using ArchiveMaster.ViewModels;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace ArchiveMaster.Views
{
    public class TwoStepPanelBase : PanelBase
    {
        public static readonly StyledProperty<object> ConfigsContentProperty =
            AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(ConfigsContent));

        public static readonly StyledProperty<object> ExecuteButtonContentProperty
            = AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(ExecuteButtonContent), "执行");

        public static readonly StyledProperty<object> InitializeButtonContentProperty
            = AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(InitializeButtonContent), "初始化");

        public static readonly StyledProperty<object> ResetButtonContentProperty
            = AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(ResetButtonContent), "重置");

        public static readonly StyledProperty<object> ResultsContentProperty =
            AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(ResultsContent));

        public static readonly StyledProperty<object> StopButtonContentProperty
            = AvaloniaProperty.Register<TwoStepPanelBase, object>(nameof(StopButtonContent), "取消");

        private Grid bottomOneLine;

        private Grid bottomTwoLine;

        private ScrollViewer configGrid;

        private Grid container;

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

        public object StopButtonContent
        {
            get => GetValue(StopButtonContentProperty);
            set => SetValue(StopButtonContentProperty, value);
        }

        protected override Type StyleKeyOverride { get; } = typeof(TwoStepPanelBase);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            var gridSplitter = e.NameScope.Find<GridSplitter>("PART_GridSplitter");
            configGrid = e.NameScope.Find<ScrollViewer>("PART_Configs");
            bottomOneLine = e.NameScope.Find<Grid>("PART_BottomOneLine");
            bottomTwoLine = e.NameScope.Find<Grid>("PART_BottomTwoLine");
            container = e.NameScope.Find<Grid>("PART_ContentContainer");
            if (gridSplitter == null || configGrid == null || bottomOneLine == null || bottomTwoLine == null)
            {
                throw new InvalidOperationException($"{nameof(TwoStepPanelBase)}的模板不对应");
            }

            gridSplitter.DragStarted += (_, _) =>
            {
                //开始拖动后，解除相关限制。
                //1.取消滚动区高度限制
                //2.配置单元格实际高度为当前高度，避免闪现
                //3.配置单元格最大高度为需要的高度
                configGrid.MaxHeight = double.MaxValue;
                container.RowDefinitions[0].Height =
                    new GridLength(container.RowDefinitions[0].ActualHeight, GridUnitType.Pixel);
                container.RowDefinitions[0].MaxHeight = configGrid.Extent.Height + 16;
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
                configGrid.MaxHeight = Bounds.Height > 700 ? 300d : 200d;
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