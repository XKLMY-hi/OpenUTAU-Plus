using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OpenUtau.App.Controls;

/// <summary>
/// FL Studio 同款旋钮：指针角度表示 Pan（-100 左 / 0 中 / +100 右）。
/// 垂直拖拽调节（向上增大，满程约 240px），双击复位 0。
/// </summary>
public partial class PanKnob : UserControl {
    private const double MaxAngle = 135;   // 指针最大偏转角度（度）
    private const double DragRange = 240;  // 满程拖动像素

    private double value;
    /// <summary>当前值（-100..100）。</summary>
    public double Value {
        get => value;
        set {
            double v = Math.Clamp(value, -100, 100);
            if (Math.Abs(v - this.value) < 0.001) {
                return;
            }
            this.value = v;
            UpdateNeedle();
            ValueChanged?.Invoke(this, v);
        }
    }
    public event EventHandler<double>? ValueChanged;

    private double dragStartX;
    private double dragStartY;
    private double dragStartValue;
    private bool lastRightSide;
    private TopLevel? trackingRoot;

    public PanKnob() {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateNeedle();
    }

    /// <summary>
    /// 按下开始全局追踪：控件仅 26px，拖出控件边界后仍跟随鼠标。
    /// 用 Tunnel + handledEventsToo 挂在窗口根部（混音台内其他控件标记
    /// Handled 也不截断），Tunnel 阶段设 Handled 阻止 ScrollViewer 滚动。
    /// ⚠️ 不用 Pointer.Capture——ScrollViewer 拖动时会抢捕获，
    /// CaptureLost 会中断追踪（实测"拖不动"根因）。
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            return;
        }
        dragStartValue = value;
        lastRightSide = e.GetPosition(this).X >= Bounds.Width / 2;
        var topLevel = TopLevel.GetTopLevel(this);
        trackingRoot = topLevel;
        if (topLevel != null) {
            var startPos = e.GetPosition(topLevel);
            dragStartX = startPos.X;
            dragStartY = startPos.Y;
            topLevel.AddHandler(PointerMovedEvent, OnGlobalMoved,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            topLevel.AddHandler(PointerReleasedEvent, OnGlobalReleased,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }
        e.Handled = true;
    }

    /// <summary>
    /// FL Studio 侧向拖动：鼠标在旋钮左半区 → 向下/向左拖动增大左声道（值减小），
    /// 右半区 → 向下/向右拖动增大右声道（值增大）。水平+垂直位移都生效；
    /// 左右侧实时检测，穿过中心立即切换；切换侧保留当前值（重置基准不跳变）。
    /// </summary>
    private void OnGlobalMoved(object? sender, PointerEventArgs e) {
        if (trackingRoot == null) {
            return;
        }
        bool rightSide = e.GetPosition(this).X >= Bounds.Width / 2;
        if (rightSide != lastRightSide) {
            // 切换侧：当前值/位置成为新基准，避免方向反转导致数值跳变
            dragStartValue = value;
            dragStartX = e.GetPosition(trackingRoot).X;
            dragStartY = e.GetPosition(trackingRoot).Y;
            lastRightSide = rightSide;
            return;
        }
        var pos = e.GetPosition(trackingRoot);
        double dx = pos.X - dragStartX;
        double dy = pos.Y - dragStartY;
        // 右半区：向右(dx+)或向下(dy+) → 增大；
        // 左半区：向左(dx-)或向下(dy+) → 减小（更左），向右 → 回中
        double delta = rightSide ? (dy + dx) : (dx - dy);
        Value = dragStartValue + delta * (100.0 / DragRange);
        e.Handled = true;
    }

    private void OnGlobalReleased(object? sender, PointerReleasedEventArgs e) {
        EndTracking();
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e) {
        EndTracking();
    }

    private void EndTracking() {
        if (trackingRoot != null) {
            trackingRoot.RemoveHandler(PointerMovedEvent, OnGlobalMoved);
            trackingRoot.RemoveHandler(PointerReleasedEvent, OnGlobalReleased);
            trackingRoot = null;
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e) {
        Value = 0;
        e.Handled = true;
    }

    private void UpdateNeedle() {
        double cx = Bounds.Width / 2;
        double cy = Bounds.Height / 2;
        double radius = Math.Min(Bounds.Width, Bounds.Height) / 2 - 3.5;
        double angle = value / 100.0 * MaxAngle * Math.PI / 180.0;
        Needle.StartPoint = new Point(cx, cy);
        Needle.EndPoint = new Point(
            cx + radius * Math.Sin(angle),
            cy - radius * Math.Cos(angle));
    }
}
