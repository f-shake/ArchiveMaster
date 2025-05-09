﻿using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ArchiveMaster.Views;

public class SmoothScrollBehavior : AvaloniaObject
{
    // 1. 注册附加属性：Owner=SmoothScrollBehavior, Target=Control, Value=bool
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<
            SmoothScrollBehavior, // Owner
            Control, // 可以附加到 Control（含 ScrollViewer、DataGrid 等）上
            bool>("IsEnabled", // 名称
            defaultValue: false); // 默认 false

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    // 存每个控件的滚动状态
    class State
    {
        public bool InertiaRunning;
        public double Velocity;
        public Stopwatch SW = new Stopwatch();
        public TimeSpan Last;
    }

    private static readonly ConcurrentDictionary<ScrollViewer, State> _states = new();

    static SmoothScrollBehavior()
    {
        // 当任何 Control 上的 IsEnabled 变化时进来
        IsEnabledProperty.Changed.AddClassHandler<Control>((ctrl, e) =>
        {
            if (e.NewValue is true)
            {
                Attach(ctrl);
            }
            else
            {
                Detach(ctrl);
            }
        });
    }

    private static void Attach(Control ctrl)
    {
        // 找到或等它加载后再找 ScrollViewer
        if (ctrl is ScrollViewer sv)
        {
            Hook(sv);
        }
        else
        {
            // DataGrid、ListBox、TextBox… 模板里都带 ScrollViewer
            ctrl.GetObservable(Visual.IsVisibleProperty).Subscribe(_ =>
            {
                var inner = ctrl.FindDescendantOfType<ScrollViewer>();
                if (inner != null)
                    Hook(inner);
            });
        }
    }

    private static void Detach(Control ctrl)
    {
        if (ctrl is ScrollViewer sv)
            Unhook(sv);
        else if (ctrl.FindDescendantOfType<ScrollViewer>() is ScrollViewer inner)
            Unhook(inner);
    }

    private static void Hook(ScrollViewer sv)
    {
        // 防重复
        if (_states.ContainsKey(sv))
            return;

        var state = new State();
        _states[sv] = state;

        // handledEventsToo: true 确保能拦截已处理滚轮
        sv.AddHandler(InputElement.PointerWheelChangedEvent,
            (s, e) => OnWheel(sv, e, state),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private static void Unhook(ScrollViewer sv)
    {
        _states.TryRemove(sv, out _);
        // 不必移除 handler，因为 Behavior 生命周期同控件
    }

    /// <summary>
    /// 控制每次鼠标滚轮刻度带来的“初始速度”大小
    /// </summary>
    public static double ScrollStep { get; set; } = 8;

    /// <summary>
    /// 控制惯性衰减的速度
    /// </summary>
    public static double Friction { get; set; } = 0.015;

    /// <summary>
    /// 最小速度阈值
    /// </summary>
    public static double MinVel { get; set; } = 0.02;

    private static void OnWheel(ScrollViewer sv, PointerWheelEventArgs e, State state)
    {
        e.Handled = true;

        // 注入速度
        var deltaVel = -e.Delta.Y * ScrollStep / 16.0;
        state.Velocity += deltaVel;

        if (!state.InertiaRunning)
            StartInertia(sv, state);
    }

    private static void StartInertia(ScrollViewer sv, State state)
    {
        state.InertiaRunning = true;
        state.SW.Restart();
        state.Last = state.SW.Elapsed;

        void Loop()
        {
            var now = state.SW.Elapsed;
            var dt = (now - state.Last).TotalMilliseconds;
            state.Last = now;

            // 摩擦衰减
            state.Velocity /= (1 + Friction * dt);

            var dy = state.Velocity * dt;
            var from = sv.Offset.Y;
            var max = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
            var to = Math.Clamp(from + dy, 0, max);

            // 如果到边界还在往外滚，就清零速度
            if ((to == 0 && dy < 0) || (to == max && dy > 0))
                state.Velocity = 0;

            sv.Offset = sv.Offset.WithY(to);

            if (Math.Abs(state.Velocity) > MinVel)
                Dispatcher.UIThread.Post(Loop, DispatcherPriority.Background);
            else
                state.InertiaRunning = false;
        }

        Dispatcher.UIThread.Post(Loop, DispatcherPriority.Background);
    }
}