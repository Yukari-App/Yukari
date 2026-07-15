using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Yukari.Views.Controls;

public sealed partial class ShadowedText : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(ShadowedText),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
        nameof(OutlineBrush),
        typeof(Brush),
        typeof(ShadowedText),
        new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush OutlineBrush
    {
        get => (Brush)GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public ShadowedText() => InitializeComponent();
}
