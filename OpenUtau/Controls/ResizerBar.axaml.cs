using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenUtau.App.Controls {
    /// <summary>
    /// Reusable drag-to-resize bar.  Replaces the GridSplitter (piano roll) and
    /// manual Pointer drag (mixer) with a single, testable, ToolTip-capable control.
    /// Attach to a parent Grid row containing the ResizerBar and set Min/Max.
    /// </summary>
    public partial class ResizerBar : UserControl {
        public static readonly StyledProperty<Orientation> DragOrientationProperty =
            AvaloniaProperty.Register<ResizerBar, Orientation>(nameof(DragOrientation), Orientation.Vertical);
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<ResizerBar, double>(nameof(Minimum), 120);
        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<ResizerBar, double>(nameof(Maximum), 600);
        public static readonly StyledProperty<string?> ToolTipCaptionProperty =
            AvaloniaProperty.Register<ResizerBar, string?>(nameof(ToolTipCaption), null);

        public Orientation DragOrientation { get => GetValue(DragOrientationProperty); set => SetValue(DragOrientationProperty, value); }
        public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public string? ToolTipCaption { get => GetValue(ToolTipCaptionProperty); set => SetValue(ToolTipCaptionProperty, value); }

        public event EventHandler<double>? DragChanged;

        private bool _dragging;
        private double _startPos;
        private Border? _tooltip;
        private double _startValue;

        public ResizerBar() {
            InitializeComponent();
            Bar.PointerPressed += OnPressed;
            Bar.PointerMoved += OnMoved;
            Bar.PointerReleased += OnReleased;
            Bar.PointerCaptureLost += OnCaptureLost;
        }

        void OnPressed(object? s, PointerPressedEventArgs e) {
            if (Parent is not Grid grid) return;
            _dragging = true;
            var pt = e.GetPosition((Visual?)Parent);
            _startPos = DragOrientation == Orientation.Vertical ? pt.Y : pt.X;
            int row = Grid.GetRow(this);
            if (row <= 0) return;
            _startValue = DragOrientation == Orientation.Vertical
                ? grid.RowDefinitions[row - 1].Height.Value
                : grid.ColumnDefinitions[row - 1].Width.Value;
            e.Pointer.Capture(Bar);
            e.Handled = true;
            ShowTooltip(_startValue);
        }

        void OnMoved(object? s, PointerEventArgs e) {
            if (!_dragging || Parent is not Grid grid) return;
            var pt = e.GetPosition((Visual?)Parent);
            double cur = DragOrientation == Orientation.Vertical ? pt.Y : pt.X;
            double delta = _startPos - cur;
            double newVal = Math.Clamp(_startValue + delta, Minimum, Maximum);
            int row = Grid.GetRow(this);
            if (DragOrientation == Orientation.Vertical)
                grid.RowDefinitions[row - 1].Height = new GridLength(newVal);
            else
                grid.ColumnDefinitions[row - 1].Width = new GridLength(newVal);
            UpdateTooltip(newVal);
            e.Handled = true;
        }

        void OnReleased(object? s, PointerEventArgs e) {
            _dragging = false;
            e.Pointer.Capture(null);
            HideTooltip();
            if (Parent is Grid grid) {
                int row = Grid.GetRow(this);
                if (row > 0) {
                    double val = DragOrientation == Orientation.Vertical
                        ? grid.RowDefinitions[row - 1].Height.Value
                        : grid.ColumnDefinitions[row - 1].Width.Value;
                    DragChanged?.Invoke(this, val);
                }
            }
        }

        void OnCaptureLost(object? s, PointerCaptureLostEventArgs e) {
            _dragging = false;
            HideTooltip();
        }

        void ShowTooltip(double value) {
            if (string.IsNullOrEmpty(ToolTipCaption)) return;
            _tooltip = new Border {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4),
                Child = new TextBlock {
                    Text = $"{ToolTipCaption}: {value:F0}px",
                    FontSize = 11,
                    Foreground = Brushes.White,
                },
            };
            var parent = this.Parent as Panel;
            parent?.Children.Add(_tooltip);
        }

        void UpdateTooltip(double value) {
            if (_tooltip?.Child is TextBlock tb)
                tb.Text = $"{ToolTipCaption}: {value:F0}px";
        }

        void HideTooltip() {
            if (_tooltip != null && _tooltip.Parent is Panel p)
                p.Children.Remove(_tooltip);
            _tooltip = null;
        }
    }
}
