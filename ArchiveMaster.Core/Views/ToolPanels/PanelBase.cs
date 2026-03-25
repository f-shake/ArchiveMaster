using Avalonia;
using Avalonia.Controls;
using System;
using ArchiveMaster.ViewModels;
using Avalonia.Controls.Primitives;

namespace ArchiveMaster.Views
{
    public class PanelBase : TemplatedControl
    {
        protected override Type StyleKeyOverride { get; } = typeof(PanelBase);
        
        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<PanelBase, string>(nameof(Description));

        public static readonly StyledProperty<object> PanelContentProperty =
            AvaloniaProperty.Register<PanelBase, object>(nameof(PanelContent));

        public static readonly StyledProperty<object> RightTopContentProperty =
            AvaloniaProperty.Register<PanelBase, object>(nameof(RightTopContent));

        public static readonly StyledProperty<string> TitleProperty =
                            AvaloniaProperty.Register<PanelBase, string>(nameof(Title));
        
        
        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object PanelContent
        {
            get => GetValue(PanelContentProperty);
            set => SetValue(PanelContentProperty, value);
        }

        public object RightTopContent
        {
            get => GetValue(RightTopContentProperty);
            set => SetValue(RightTopContentProperty, value);
        }
        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
    }
}
