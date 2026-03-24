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

        // 新增：下划线属性
        public static readonly StyledProperty<bool> UnderlineProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, bool>(nameof(Underline), false);

        // 新增：删除线属性
        public static readonly StyledProperty<bool> StrikethroughProperty =
            AvaloniaProperty.Register<OutlinedTextBlock, bool>(nameof(Strikethrough), false);

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
        public bool Underline { get => GetValue(UnderlineProperty); set => SetValue(UnderlineProperty, value); }
        public bool Strikethrough { get => GetValue(StrikethroughProperty); set => SetValue(StrikethroughProperty, value); }
        #endregion

        static OutlinedTextBlock()
        {
            AffectsRender<OutlinedTextBlock>(FillProperty, StrokeProperty, StrokeThicknessProperty, UnderlineProperty, StrikethroughProperty);
            AffectsMeasure<OutlinedTextBlock>(TextProperty, FontFamilyProperty, FontSizeProperty, 
                FontStyleProperty, FontWeightProperty, TextAlignmentProperty, UnderlineProperty, StrikethroughProperty);
            
            StrokeProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x._pen = null);
            StrokeThicknessProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x._pen = null);
            
            TextProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            FontFamilyProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            FontSizeProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            UnderlineProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
            StrikethroughProperty.Changed.AddClassHandler<OutlinedTextBlock>((x, _) => x.InvalidateText());
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
            
            var thickness = StrokeThickness;
            return new Size(_formattedText.Width + thickness, _formattedText.Height + thickness);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textGeometry = null; 
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
                var offset = StrokeThickness / 2;
                using (context.PushTransform(Matrix.CreateTranslation(offset, offset)))
                {
                    // 注意：这里先画描边（Pen），再画填充（Fill），保证文字内部清晰
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
                MaxTextHeight = Math.Max(1, constraint.Height),
                TextAlignment = TextAlignment
            };
        }

        private void EnsureGeometry()
        {
            if (_textGeometry != null || _formattedText == null) return;

            // 获取基础文字路径
            var textGeo = _formattedText.BuildGeometry(new Point(0, 0));
            
            // 手动构建装饰线几何图形
            Geometry? decorations = null;
            double thickness = Math.Max(1, FontSize / 15); // 根据字号自动计算线条粗细

            // 处理下划线
            if (Underline)
            {
                // 位置通常在基线下方一点点
                double y = _formattedText.Baseline + (thickness * 1.5);
                var rect = new RectangleGeometry(new Rect(0, y, _formattedText.Width, thickness));
                decorations = rect;
            }

            // 处理删除线
            if (Strikethrough)
            {
                // 位置通常在基线上方约 1/3 字高处
                double y = _formattedText.Baseline - (FontSize * 0.3);
                var rect = new RectangleGeometry(new Rect(0, y, _formattedText.Width, thickness));
                
                if (decorations == null) decorations = rect;
                else decorations = new CombinedGeometry(GeometryCombineMode.Union, decorations, rect);
            }

            // 合并文字和装饰线
            if (decorations != null)
            {
                _textGeometry = new CombinedGeometry(GeometryCombineMode.Union, textGeo, decorations);
            }
            else
            {
                _textGeometry = textGeo;
            }
        }
    }
}