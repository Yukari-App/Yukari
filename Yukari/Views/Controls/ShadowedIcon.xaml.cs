using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Yukari.Views.Controls;

public sealed partial class ShadowedIcon : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(ShadowedIcon),
        new PropertyMetadata(string.Empty)
    );

    public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.Register(
        nameof(OutlineBrush),
        typeof(Brush),
        typeof(ShadowedIcon),
        new PropertyMetadata(new SolidColorBrush(Colors.Transparent))
    );

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Brush OutlineBrush
    {
        get => (Brush)GetValue(OutlineBrushProperty);
        set => SetValue(OutlineBrushProperty, value);
    }

    public ShadowedIcon()
    {
        InitializeComponent();
        RegisterPropertyChangedCallback(IsEnabledProperty, OnIsEnabledChanged);
    }

    private void OnIsEnabledChanged(DependencyObject sender, DependencyProperty dp) =>
        RootGrid.Opacity = IsEnabled ? 1.0 : 0.40;
}
