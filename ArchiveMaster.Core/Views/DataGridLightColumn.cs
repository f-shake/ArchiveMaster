using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Utils;
using Avalonia.Interactivity;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace ArchiveMaster.Views
{
    public class DataGridLightColumn : DataGridColumn
    {
        public DataGridLightColumn()
        {
            CanUserResize = false;
            CanUserReorder = false;
        }

        public static readonly StyledProperty<IBrush> FillProperty =
            AvaloniaProperty.Register<DataGridLightColumn, IBrush>(
                nameof(Fill), Brushes.Red);

        public IBrush Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public IBinding VisibleBinding { get; set; }

        public IBinding FillBinding { get; set; }

        public override bool IsReadOnly
        {
            get => true;
            set => throw new InvalidOperationException("必须为只读类型");
        }


        protected override Control GenerateEditingElement(DataGridCell cell, object dataItem,
            out ICellEditBinding binding)
        {
            binding = null;
            return null;
        }

        protected override Control GenerateElement(DataGridCell cell, object dataItem)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Fill
            };

            if (FillBinding != null)
            {
                ellipse.Bind(Shape.FillProperty, FillBinding);
            }

            if (VisibleBinding != null)
            {
                ellipse.Bind(Visual.IsVisibleProperty, VisibleBinding);
            }

            return ellipse;
        }

        protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
        {
            return editingElement;
        }
    }
}