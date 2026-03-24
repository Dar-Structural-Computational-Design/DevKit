using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevKit.Views
{
    public class LineNumberMargin : FrameworkElement
    {
        private TextBox _target;

        public static readonly DependencyProperty TargetTextBoxProperty =
            DependencyProperty.Register("TargetTextBox", typeof(TextBox), typeof(LineNumberMargin),
                new PropertyMetadata(null, OnTargetChanged));

        public TextBox TargetTextBox { get => (TextBox)GetValue(TargetTextBoxProperty); set => SetValue(TargetTextBoxProperty, value); }

        private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var m = (LineNumberMargin)d;
            if (e.OldValue is TextBox old) { old.TextChanged -= m.OnChanged; old.SizeChanged -= m.OnSized; }
            if (e.NewValue is TextBox tb) m.Attach(tb);
        }

        private void Attach(TextBox tb)
        {
            _target = tb;
            _target.TextChanged += OnChanged;
            _target.SizeChanged += OnSized;
            _target.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnScrolled));
            Width = 45;
            InvalidateVisual();
        }

        private void OnChanged(object s, TextChangedEventArgs e) => InvalidateVisual();
        private void OnSized(object s, SizeChangedEventArgs e) => InvalidateVisual();
        private void OnScrolled(object s, ScrollChangedEventArgs e) => InvalidateVisual();

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (_target == null) return;

            var gutterBrush = TryFindResource("LineGutter") as Brush
                              ?? new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x22));
            var numBrush = TryFindResource("TextDim") as Brush
                           ?? new SolidColorBrush(Color.FromRgb(0x58, 0x5B, 0x70));

            dc.DrawRectangle(gutterBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

            string text = _target.Text ?? "";
            int lineCount = 1;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') lineCount++;

            double vertOff = 0;
            var sv = FindSV(_target);
            if (sv != null) vertOff = sv.VerticalOffset;

            var measure = new FormattedText("Ag", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(_target.FontFamily, _target.FontStyle, _target.FontWeight, _target.FontStretch),
                _target.FontSize, Brushes.White, 1.0);
            double lineH = measure.Height;

            var typeface = new Typeface(_target.FontFamily, _target.FontStyle, _target.FontWeight, _target.FontStretch);
            double topPad = _target.Padding.Top + _target.BorderThickness.Top + 1;

            for (int i = 1; i <= lineCount; i++)
            {
                double y = topPad + (i - 1) * lineH - vertOff;
                if (y < -lineH || y > ActualHeight + lineH) continue;
                var ft = new FormattedText(i.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, _target.FontSize * 0.85, numBrush, 1.0);
                dc.DrawText(ft, new Point(ActualWidth - ft.Width - 8, y));
            }
        }

        private static ScrollViewer FindSV(DependencyObject obj)
        {
            if (obj is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var r = FindSV(VisualTreeHelper.GetChild(obj, i)); if (r != null) return r; }
            return null;
        }
    }
}
