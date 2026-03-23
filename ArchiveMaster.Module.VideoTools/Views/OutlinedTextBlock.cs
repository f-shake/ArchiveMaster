using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;

namespace ArchiveMaster.Views
{
    public class OutlinedTextBlock : Control
    {
        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, string?>(nameof(Text), string.Empty);

        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, IBrush?>(nameof(Fill), Brushes.Black);

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, IBrush?>(nameof(Stroke), Brushes.Black);

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, double>(nameof(StrokeThickness), 1.0);

        public static readonly StyledProperty<FontFamily> FontFamilyProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, FontFamily>(nameof(FontFamily), FontFamily.Default);

        public static readonly StyledProperty<double> FontSizeProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, double>(nameof(FontSize), 12.0);

        public static readonly StyledProperty<FontStyle> FontStyleProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, FontStyle>(nameof(FontStyle), FontStyle.Normal);

        public static readonly StyledProperty<FontWeight> FontWeightProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, FontWeight>(nameof(FontWeight), FontWeight.Normal);

        public static readonly StyledProperty<TextAlignment> TextAlignmentProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, TextAlignment>(nameof(TextAlignment), TextAlignment.Left);

        private FormattedText? _formattedText;
        private Geometry? _textGeometry;
        private IPen? _pen;

        #region CLR Properties
        [Content]
        public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontStyle FontStyle { get => GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
        #endregion

        static OutlinedTextBlock()
        {
            // 监视属性变化，自动触发重绘或重新测量
            AffectsRender<OutlinedTextBlock>(FillProperty, StrokeProperty, StrokeThicknessProperty);
            AffectsMeasure<OutlinedTextBlock>(TextProperty, FontFamilyProperty, FontSizeProperty, 
                FontStyleProperty, FontWeightProperty, TextAlignmentProperty);
            
            // 当笔刷相关属性变化时，清除 Pen 缓存
            StrokeProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x._pen = null);
            StrokeThicknessProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x._pen = null);
            
            // 当文本相关属性变化时，清除 FormattedText 和 Geometry 缓存
            TextProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            FontFamilyProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            FontSizeProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
        }

        private void InvalidateText()
        {
            _formattedText = null;
            _textGeometry = null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            EnsureFormattedText(availableSize);
            if (_formattedText == null) return new Size();
            
            // 考虑描边粗细对尺寸的影响（可选）
            var thickness = StrokeThickness;
            return new Size(_formattedText.Width + thickness, _formattedText.Height + thickness);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textGeometry = null; // 尺寸变了，Geometry 必须重构
            return base.ArrangeOverride(finalSize);
        }

        public override void Render(DrawingContext context)
        {
            if (string.IsNullOrEmpty(Text)) return;

            EnsureFormattedText(Bounds.Size);
            EnsureGeometry();
            UpdatePen();

            if (_textGeometry != null)
            {
                // 使用平移确保描边不被边缘裁切（视需求而定）
                using (context.PushTransform(Matrix.CreateTranslation(StrokeThickness / 2, StrokeThickness / 2)))
                {
                    context.DrawGeometry(null, _pen, _textGeometry);
                    context.DrawGeometry(Fill, null, _textGeometry);
                }
            }
        }

        private void UpdatePen()
        {
            if (_pen == null)
            {
                _pen = new Pen(Stroke, StrokeThickness, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);
            }
        }

        private void EnsureFormattedText(Size constraint)
        {
            if (_formattedText != null) return;

            _formattedText = new FormattedText(
                Text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight),
                FontSize,
                Brushes.Black
            )
            {
                MaxTextWidth = constraint.Width,
                MaxTextHeight = constraint.Height,
                TextAlignment = TextAlignment
            };
        }

        private void EnsureGeometry()
        {
            if (_textGeometry != null || _formattedText == null) return;
            _textGeometry = _formattedText.BuildGeometry(new Point(0, 0));
        }
    }
}