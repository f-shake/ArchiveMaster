using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ArchiveMaster.Views;
    public abstract class SVirtualizingPanel : VirtualizingPanel, IScrollSnapPointsInfo
    {

        public static readonly StyledProperty<ICommand> LoadMoreProperty =
  AvaloniaProperty.Register<SVirtualizingPanel, ICommand?>(nameof(LoadMore));

        public ICommand? LoadMore
        {
            get { return GetValue(LoadMoreProperty); }
            set { SetValue(LoadMoreProperty, value); }
        }


        public static readonly StyledProperty<Boolean> IsReachEndProperty =
AvaloniaProperty.Register<SVirtualizingPanel, Boolean>(nameof(IsReachEnd));

        public Boolean IsReachEnd
        {
            get { return GetValue(IsReachEndProperty); }
            set { SetValue(IsReachEndProperty, value); }
        }
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            ScrollToLoadMore();
        }

        public abstract bool AreHorizontalSnapPointsRegular { get; set; }
        public abstract bool AreVerticalSnapPointsRegular { get; set; }

        public abstract event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged;
        public abstract event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged;

        public abstract IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment);
        public abstract double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset);

        public Boolean IsLoadingMore { get; protected set; } = false;

        protected int _currentIndex = 0;
        protected int _lastIndex = 0;
        private const string DefaultRecycleKey = "SVirtualizingPanel";

        protected abstract void ScrollToLoadMore();
        protected void OnLoadMore()
        {
            if (IsLoadingMore || IsReachEnd)
                return;

            IsLoadingMore = true;
            try
            {
                LoadMore?.Execute(null);
            }
            catch (Exception)
            {
                // 可以加日志记录
            }
            finally
            {
                IsLoadingMore = false;
            }
        }
        protected class ElementRenderModel
        {
            public Control? Control { get; set; } = null;
            public double Left { get; set; } = 0;
            public double Top { get; set; } = 0;

            public double Width { get; set; } = 0;

            public double Height { get; set; } = 0;
            public Boolean IsRendered { get; set; } = false;

        }
        protected Dictionary<int, ElementRenderModel> _elementDictionary = new Dictionary<int, ElementRenderModel>();
        protected Rect _effectiveViewport = new Rect(0, -1, 0, 0);
        protected double _maximumItemHeight = 0.0;
        protected double _maximumItemWidth = 0.0;
        protected Size _panelSize = new Size();
        protected double _currentLineWidth = 0;
        protected double _currentLineHeight = 0;
        protected const double VirtualizationCacheLength = 300;

        protected abstract int RenderElements(int startIndex);
        //protected abstract Boolean IsMeasureFinished(Control control);

        protected void UpdateMaximumElementSize(Size desiredSize)
        {
            _maximumItemWidth = Math.Max(_maximumItemWidth, desiredSize.Width);
            _maximumItemHeight = Math.Max(_maximumItemHeight, desiredSize.Height);
        }

        protected void ClearElementRange(int startIndex)
        {
            if (_elementDictionary.Count == 0)
            {
                return;
            }

            var keysToRemove = new List<int>();
            foreach (var key in _elementDictionary.Keys)
            {
                if (key >= startIndex)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_elementDictionary.TryGetValue(key, out var element))
                {
                    if (element.Control is { } control)
                    {
                        RemoveInternalChild(control);
                        ItemContainerGenerator?.ClearItemContainer(control);
                    }
                }

                _elementDictionary.Remove(key);
            }
        }

        protected void ClearElementsOutsideRange(int startIndex, int endIndex)
        {
            if (_elementDictionary.Count == 0)
            {
                return;
            }

            var keysToRemove = new List<int>();
            foreach (var key in _elementDictionary.Keys)
            {
                if (key < startIndex || key > endIndex)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_elementDictionary.TryGetValue(key, out var element) && element.Control is { } control)
                {
                    RemoveInternalChild(control);
                    ItemContainerGenerator?.ClearItemContainer(control);
                    element.Control = null;
                    element.IsRendered = false;
                }
            }
        }

        protected double GetVerticalViewportStart()
        {
            return Math.Max(0, _effectiveViewport.Top - Math.Max(VirtualizationCacheLength, _maximumItemHeight));
        }

        protected double GetVerticalViewportEnd()
        {
            return _effectiveViewport.Top + _effectiveViewport.Height + Math.Max(VirtualizationCacheLength, _maximumItemHeight);
        }

        protected double GetHorizontalViewportStart()
        {
            return Math.Max(0, _effectiveViewport.Left - Math.Max(VirtualizationCacheLength, _maximumItemWidth));
        }

        protected double GetHorizontalViewportEnd()
        {
            return _effectiveViewport.Left + _effectiveViewport.Width + Math.Max(VirtualizationCacheLength, _maximumItemWidth);
        }

        protected static double GetSnapPointValue(double start, double size, SnapPointsAlignment alignment)
        {
            return alignment switch
            {
                SnapPointsAlignment.Center => start + size / 2,
                SnapPointsAlignment.Far => start + size,
                _ => start
            };
        }

        protected static RoutedEventArgs CreateSnapPointsChangedEventArgs()
        {
            return new RoutedEventArgs();
        }

        protected Control CreateVirtualizingElement(object item, int index, string recycleKey = DefaultRecycleKey)
        {
            var _generator = ItemContainerGenerator!;
            var _container = _generator.CreateContainer(item, index, recycleKey);
            _generator.PrepareItemContainer(_container, item, index);
            AddInternalChild(_container);
            _generator.ItemContainerPrepared(_container, item, index);

            _container.Measure(Size.Infinity);
            UpdateMaximumElementSize(_container.DesiredSize);
            return _container;
        }
    }
    public sealed class SVirtualizingWrapPanel : SVirtualizingPanel
    {
        public override bool AreHorizontalSnapPointsRegular { get => false; set { } }
        public override bool AreVerticalSnapPointsRegular { get => false; set { } }

        public override event EventHandler<RoutedEventArgs>? HorizontalSnapPointsChanged;
        public override event EventHandler<RoutedEventArgs>? VerticalSnapPointsChanged;

        public SVirtualizingWrapPanel()
        {
            this.EffectiveViewportChanged += VirtualizingWrapPanel_EffectiveViewportChanged;
        }
        Boolean _isLoadRendered = false;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
            {
                HorizontalSnapPointsChanged?.Invoke(this, CreateSnapPointsChangedEventArgs());
                VerticalSnapPointsChanged?.Invoke(this, CreateSnapPointsChangedEventArgs());
            }
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (!_isLoadRendered)
            {
                RenderElements(_currentIndex);
            }
        }
        private void VirtualizingWrapPanel_EffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            //Debug.WriteLine(e.EffectiveViewport.Height);
            //Debug.WriteLine(e.EffectiveViewport.Top);            
            if (e.EffectiveViewport.Top == -1 || e.EffectiveViewport.Width == 0 || e.EffectiveViewport.Height == 0)
            {
                return;
            }
            if (e.EffectiveViewport.Top != _effectiveViewport.Top || e.EffectiveViewport.Width != _effectiveViewport.Width || e.EffectiveViewport.Height != _effectiveViewport.Height)
            {
                _effectiveViewport = e.EffectiveViewport;
                //Debug.WriteLine($"Top:{_EffectiveViewport.Top}");
                #region//获取进入渲染位置的第一个index
                var viewportStart = GetVerticalViewportStart();
                var _firstIndex = 0;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (_elementDictionary.TryGetValue(i, out var _element))
                    {
                        if (_element.Top + _element.Height > viewportStart || _element.Top + _maximumItemHeight > viewportStart)
                        {
                            _firstIndex = i;
                            break;
                        }
                    }
                }
                //Debug.WriteLine("firstIndex:" + _firstIndex);
                #endregion
                #region//获取该进行渲染的第一个index
                var _startIndex = 0;
                for (int i = _firstIndex; i >= 0; i--)
                {
                    if (_elementDictionary.TryGetValue(i, out var _element))
                    {
                        if (_element.Left == 0)
                        {
                            _startIndex = i;
                            break;
                        }
                    }
                }
                _currentIndex = _startIndex;
                //Debug.WriteLine("startIndex:" + _startIndex);
                #endregion
                #region//正式渲染                               
                _lastIndex = RenderElements(_startIndex);
                //Debug.WriteLine("lastIndex:" + _LastIndex);
                #endregion
                #region//回收其他元素
                for (int i = 0; i < Items.Count; i++)
                {
                    if (i < _startIndex || i > _lastIndex)
                    {
                        if (_elementDictionary.TryGetValue(i, out var _element))
                        {
                            if (_element.Control is { } && ItemContainerGenerator is { })
                            {
                                RemoveInternalChild(_element.Control);
                                ItemContainerGenerator.ClearItemContainer(_element.Control);
                                _element.Control = null;
                                _element.IsRendered = false;
                                //Debug.WriteLine($"回收{i}");
                            }
                        }
                    }
                }
                #endregion
                InvalidateMeasure();
                InvalidateArrange();
                ScrollToLoadMore();
                _isLoadRendered = true;
            }
            else
            {
                _effectiveViewport = e.EffectiveViewport;
            }
        }
        protected override void ScrollToLoadMore()
        {
            if (_effectiveViewport.Top + _effectiveViewport.Height >= _panelSize.Height - 300)
            {
                OnLoadMore();
            }
        }




        protected override int RenderElements(int startIndex)
        {
            var _endIndex = Items.Count - 1;
            var _index = startIndex;
            //Debug.WriteLine("_maxLineWidth" + _maxLineWidth);
            double _maxLineWidth = this.Bounds.Width;
            double _maxLineHeight = 0.0;
            if (_elementDictionary.TryGetValue(_index, out var _firstElement))
            {
                _currentLineHeight = _firstElement.Top;
            }
            else
            {
                _currentLineHeight = 0;
            }
            _currentLineWidth = 0;
            #region//先计算需渲染的每个控件所需的空间          
            for (int i = startIndex; i < Items.Count; i++)
            {
                var _item = Items[i];
                if (_item is { })
                {
                    Control? _element = null;
                    if (!_elementDictionary.TryGetValue(i, out var _value))
                    {
                        _element = CreateVirtualizingElement(_item, i);
                        var _newValue = new ElementRenderModel();
                        if (_currentLineWidth + _element.DesiredSize.Width > _maxLineWidth)
                        {
                            _currentLineHeight += _maxLineHeight;
                            _currentLineWidth = 0;
                            _maxLineHeight = 0;
                            _newValue.Top = _currentLineHeight;
                            _newValue.Left = 0;
                            _newValue.Width = _element.DesiredSize.Width;
                            _newValue.Height = _element.DesiredSize.Height;
                            _currentLineWidth = _element.DesiredSize.Width;
                        }
                        else
                        {
                            _newValue.Top = _currentLineHeight;
                            _newValue.Left = _currentLineWidth;
                            _newValue.Width = _element.DesiredSize.Width;
                            _newValue.Height = _element.DesiredSize.Height;
                            _currentLineWidth += _element.DesiredSize.Width;
                        }
                        _maxLineHeight = Math.Max(_maxLineHeight, _element.DesiredSize.Height);
                        _newValue.Control = _element;
                        _newValue.IsRendered = true;

                        _elementDictionary.Add(i, _newValue);
                    }
                    else
                    {
                        _element = _value.Control;
                        if (_element is { })
                        {
                            if (_currentLineWidth + _element.DesiredSize.Width > _maxLineWidth)
                            {
                                _currentLineHeight += _maxLineHeight;
                                _currentLineWidth = 0;
                                _maxLineHeight = 0;
                                _value.Top = _currentLineHeight;
                                _value.Left = 0;
                                _value.Width = _element.DesiredSize.Width;
                                _value.Height = _element.DesiredSize.Height;
                                _currentLineWidth = _element.DesiredSize.Width;
                            }
                            else
                            {
                                _value.Top = _currentLineHeight;
                                _value.Left = _currentLineWidth;
                                _value.Width = _element.DesiredSize.Width;
                                _value.Height = _element.DesiredSize.Height;
                                _currentLineWidth += _element.DesiredSize.Width;
                            }
                            _maxLineHeight = Math.Max(_maxLineHeight, _element.DesiredSize.Height);
                        }
                        else
                        {
                            _element = CreateVirtualizingElement(_item, i);
                            if (_currentLineWidth + _element.DesiredSize.Width > _maxLineWidth)
                            {
                                _currentLineHeight += _maxLineHeight;
                                _currentLineWidth = 0;
                                _maxLineHeight = 0;
                                _value.Top = _currentLineHeight;
                                _value.Left = 0;
                                _value.Width = _element.DesiredSize.Width;
                                _value.Height = _element.DesiredSize.Height;
                                _currentLineWidth = _element.DesiredSize.Width;
                            }
                            else
                            {
                                _value.Top = _currentLineHeight;
                                _value.Left = _currentLineWidth;
                                _value.Width = _element.DesiredSize.Width;
                                _value.Height = _element.DesiredSize.Height;
                                _currentLineWidth += _element.DesiredSize.Width;
                            }
                            _maxLineHeight = Math.Max(_maxLineHeight, _element.DesiredSize.Height);
                            _value.Control = _element;
                            _value.IsRendered = true;
                        }
                    }
                    if (_effectiveViewport.Top >= 0 && _currentLineHeight > GetVerticalViewportEnd())
                    {
                        _endIndex = i;
                        break;
                    }
                }
            }
            #endregion

            _panelSize = new Size(_effectiveViewport.Width, _currentLineHeight + _maxLineHeight);
            #region//正式Measure自身
            InvalidateMeasure();
            #endregion
            #region//正式Arrange自身
            InvalidateArrange();
            #endregion                     
            return _endIndex;
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var i in _elementDictionary)
            {
                if (i.Value.Control is { })
                {

                    i.Value.Control.Measure(new Size(i.Value.Width, i.Value.Height));

                }
            }
            return _panelSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var i in _elementDictionary)
            {
                if (i.Value.Control is { })
                {
                    i.Value.Control.Arrange(new Rect(i.Value.Left, i.Value.Top, i.Value.Width, i.Value.Height));
                }
            }
            return finalSize;
        }

        protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        var _clearStartIndex = e.NewStartingIndex >= 0 ? Math.Min(e.NewStartingIndex, _currentIndex) : _currentIndex;
                        ClearElementRange(_clearStartIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Remove:
                    {
                        var _clearStartIndex = e.OldStartingIndex >= 0 ? Math.Min(e.OldStartingIndex, _currentIndex) : _currentIndex;
                        ClearElementRange(_clearStartIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Replace:
                case NotifyCollectionChangedAction.Move:
                    {
                        var changedIndex = e.NewStartingIndex >= 0 ? e.NewStartingIndex : e.OldStartingIndex;
                        var _clearStartIndex = changedIndex >= 0 ? Math.Min(changedIndex, _currentIndex) : _currentIndex;
                        ClearElementRange(_clearStartIndex);
                        break;
                    }
                case NotifyCollectionChangedAction.Reset:
                    {
                        ClearElementRange(0);
                        break;
                    }
            }
            base.OnItemsChanged(items, e);
            RenderElements(_currentIndex);
            HorizontalSnapPointsChanged?.Invoke(this, CreateSnapPointsChangedEventArgs());
            VerticalSnapPointsChanged?.Invoke(this, CreateSnapPointsChangedEventArgs());
        }



        protected override Control? ScrollIntoView(int index)
        {
            var _items = Items;

            if (index < 0 || index >= _items.Count || !IsEffectivelyVisible)
                return null;

            var renderStartIndex = GetScrollIntoViewStartIndex(index);

            if (!_elementDictionary.ContainsKey(index))
            {
                RenderElements(renderStartIndex);
            }

            if (_elementDictionary.TryGetValue(index, out var renderModel) && renderModel.IsRendered)
            {
                if (renderModel.Control is Control _element)
                {
                    _element.BringIntoView();
                    return _element;
                }
            }
            else if (this.VisualRoot is Visual root)
            {
                RenderElements(renderStartIndex);
                if (_elementDictionary.TryGetValue(index, out renderModel) && renderModel.Control is Control _element)
                {
                    _element.UpdateLayout();
                    _element.BringIntoView();
                    return _element;
                }
            }
            return null;
        }
        protected override Control? ContainerFromIndex(int index)
        {
            if (_elementDictionary.TryGetValue(index, out var _element))
            {
                return _element.Control;
            }
            return null;
        }

        protected override int IndexFromContainer(Control container)
        {
            foreach (var i in _elementDictionary)
            {
                if (i.Value.Control == container)
                {
                    return i.Key;
                }
            }
            return -1;
        }

        protected override IEnumerable<Control>? GetRealizedContainers()
        {
            return _elementDictionary.Where(_ => _.Value.Control is { }).Select(_ => _.Value.Control).OfType<Control>().ToList();
        }

        protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
        {
            var count = Items.Count;
            var fromControl = from as Control;

            if (count == 0 ||
                (fromControl is null && direction is not NavigationDirection.First and not NavigationDirection.Last))
                return null;

            var fromIndex = fromControl != null ? IndexFromContainer(fromControl) : -1;
            var toIndex = fromIndex;

            switch (direction)
            {
                case NavigationDirection.First:
                    toIndex = 0;
                    break;
                case NavigationDirection.Last:
                    toIndex = count - 1;
                    break;
                case NavigationDirection.Next:
                    ++toIndex;
                    break;
                case NavigationDirection.Previous:
                    --toIndex;
                    break;
                case NavigationDirection.Left:
                    --toIndex;
                    break;
                case NavigationDirection.Right:
                    ++toIndex;
                    break;
                case NavigationDirection.Up:
                    --toIndex;
                    break;
                case NavigationDirection.Down:
                    ++toIndex;
                    break;
                default:
                    return null;
            }

            if (fromIndex == toIndex)
                return from;

            if (wrap)
            {
                if (toIndex < 0)
                    toIndex = count - 1;
                else if (toIndex >= count)
                    toIndex = 0;
            }

            return ScrollIntoView(toIndex);
        }

        public override IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
        {
            if (_elementDictionary.Count == 0)
            {
                return Array.Empty<double>();
            }

            return orientation == Orientation.Vertical
                ? _elementDictionary.Values
                    .GroupBy(element => element.Top)
                    .OrderBy(group => group.Key)
                    .Select(group => GetSnapPointValue(group.Key, group.Max(element => element.Height), snapPointsAlignment))
                    .ToList()
                : _elementDictionary.Values
                    .GroupBy(element => element.Left)
                    .OrderBy(group => group.Key)
                    .Select(group => GetSnapPointValue(group.Key, group.Max(element => element.Width), snapPointsAlignment))
                    .ToList();
        }

        public override double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
        {
            offset = 0;
            return 0;
        }

        private int GetScrollIntoViewStartIndex(int index)
        {
            for (int i = index; i >= 0; i--)
            {
                if (_elementDictionary.TryGetValue(i, out var element) && element.Left == 0)
                {
                    return i;
                }
            }

            return 0;
        }
    }