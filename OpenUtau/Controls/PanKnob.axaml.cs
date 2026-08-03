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

    private bool isDragging;
    private double dragStartY;
    private double dragStartValue;

    public PanKnob() {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateNeedle();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) {
            return;
        }
        isDragging = true;
        dragStartY = e.GetPosition(this).Y;
        dragStartValue = value;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e) {
        if (!isDragging) {
            return;
        }
        double dy = e.GetPosition(this).Y - dragStartY;
        Value = dragStartValue - dy * (100.0 / DragRange);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
        isDragging = false;
        e.Pointer.Capture(null);
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
