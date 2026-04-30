using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Enums;

namespace Yukari.ViewModels.Components;

public partial class ReaderDisplayContext : ObservableObject
{
    [ObservableProperty]
    public partial (double Width, double Height) ScreenSize { get; set; }

    [ObservableProperty]
    public partial ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;
}
