using Avalonia;
using Avalonia.Controls;
using System;

namespace ArchiveMaster.Views
{
    public partial class TwoStepPanelBase : PanelBase
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

        public TwoStepPanelBase()
        {
            InitializeComponent();
        }

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
    }
}