using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace SonicEddy.Controls.MixerControls;

public partial class LooperCompactSection : UserControl
{
    public static readonly StyledProperty<double> MixProperty =
        AvaloniaProperty.Register<LooperCompactSection, double>(
            nameof(Mix),
            defaultBindingMode: BindingMode.TwoWay);

    public double Mix
    {
        get => GetValue(MixProperty);
        set => SetValue(MixProperty, value);
    }

    public static readonly StyledProperty<int> SelectedBarLengthProperty =
        AvaloniaProperty.Register<LooperCompactSection, int>(
            nameof(SelectedBarLength),
            defaultBindingMode: BindingMode.TwoWay);

    public int SelectedBarLength
    {
        get => GetValue(SelectedBarLengthProperty);
        set => SetValue(SelectedBarLengthProperty, value);
    }

    public static readonly StyledProperty<int[]> BarLengthsProperty =
        AvaloniaProperty.Register<LooperCompactSection, int[]>(
            nameof(BarLengths),
            defaultValue: []);

    public int[] BarLengths
    {
        get => GetValue(BarLengthsProperty);
        set => SetValue(BarLengthsProperty, value);
    }

    public static readonly StyledProperty<bool> IsQuantizedProperty =
        AvaloniaProperty.Register<LooperCompactSection, bool>(
            nameof(IsQuantized),
            defaultBindingMode: BindingMode.TwoWay);

    public bool IsQuantized
    {
        get => GetValue(IsQuantizedProperty);
        set => SetValue(IsQuantizedProperty, value);
    }

    public static readonly StyledProperty<bool> IsPostFxSelectedProperty =
        AvaloniaProperty.Register<LooperCompactSection, bool>(
            nameof(IsPostFxSelected),
            defaultBindingMode: BindingMode.TwoWay);

    public bool IsPostFxSelected
    {
        get => GetValue(IsPostFxSelectedProperty);
        set => SetValue(IsPostFxSelectedProperty, value);
    }

    public static readonly StyledProperty<string> ActivePositionIconProperty =
        AvaloniaProperty.Register<LooperCompactSection, string>(
            nameof(ActivePositionIcon),
            defaultValue: string.Empty);

    public string ActivePositionIcon
    {
        get => GetValue(ActivePositionIconProperty);
        set => SetValue(ActivePositionIconProperty, value);
    }

    public static readonly StyledProperty<IBrush> ActionForegroundProperty =
        AvaloniaProperty.Register<LooperCompactSection, IBrush>(
            nameof(ActionForeground),
            defaultValue: Brushes.White);

    public IBrush ActionForeground
    {
        get => GetValue(ActionForegroundProperty);
        set => SetValue(ActionForegroundProperty, value);
    }

    public static readonly StyledProperty<ICommand?> CutPlayCommandProperty =
        AvaloniaProperty.Register<LooperCompactSection, ICommand?>(
            nameof(CutPlayCommand));

    public ICommand? CutPlayCommand
    {
        get => GetValue(CutPlayCommandProperty);
        set => SetValue(CutPlayCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> StopCommandProperty =
        AvaloniaProperty.Register<LooperCompactSection, ICommand?>(
            nameof(StopCommand));

    public ICommand? StopCommand
    {
        get => GetValue(StopCommandProperty);
        set => SetValue(StopCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> ShowDetailsCommandProperty =
        AvaloniaProperty.Register<LooperCompactSection, ICommand?>(
            nameof(ShowDetailsCommand));

    public ICommand? ShowDetailsCommand
    {
        get => GetValue(ShowDetailsCommandProperty);
        set => SetValue(ShowDetailsCommandProperty, value);
    }

    public LooperCompactSection()
    {
        InitializeComponent();
    }
}
