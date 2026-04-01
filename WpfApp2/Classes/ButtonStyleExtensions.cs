
using System.Windows;
using System.Windows.Media;

namespace WpfApp2
{
    public static class ButtonStyleExtensions
    {
        public static readonly DependencyProperty NormalColorProperty =
            DependencyProperty.RegisterAttached(
                "NormalColor",
                typeof(Brush),
                typeof(ButtonStyleExtensions),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(239, 165, 80))));

        public static void SetNormalColor(DependencyObject element, Brush value) =>
            element.SetValue(NormalColorProperty, value);

        public static Brush GetNormalColor(DependencyObject element) =>
            (Brush)element.GetValue(NormalColorProperty);

        public static readonly DependencyProperty HoverColorProperty =
            DependencyProperty.RegisterAttached(
                "HoverColor",
                typeof(Brush),
                typeof(ButtonStyleExtensions),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 187, 110))));

        public static void SetHoverColor(DependencyObject element, Brush value) =>
            element.SetValue(HoverColorProperty, value);

        public static Brush GetHoverColor(DependencyObject element) =>
            (Brush)element.GetValue(HoverColorProperty);

        public static readonly DependencyProperty PressedColorProperty =
            DependencyProperty.RegisterAttached(
                "PressedColor",
                typeof(Brush),
                typeof(ButtonStyleExtensions),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(255, 168, 69))));

        public static void SetPressedColor(DependencyObject element, Brush value) =>
            element.SetValue(PressedColorProperty, value);

        public static Brush GetPressedColor(DependencyObject element) =>
            (Brush)element.GetValue(PressedColorProperty);
    }
}