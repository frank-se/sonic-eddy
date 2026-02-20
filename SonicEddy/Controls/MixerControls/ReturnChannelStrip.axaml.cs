using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SonicEddy.Controls.MixerControls;

public partial class ReturnChannelStrip : UserControl
{
    /*
     * Channel header
     */
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        SelectCommandParameterProperty =
            AvaloniaProperty.Register<ReturnChannelStrip, object?>(
                nameof(SelectCommandParameter));

    public object? SelectCommandParameter
    {
        get => GetValue(SelectCommandParameterProperty);
        set => SetValue(SelectCommandParameterProperty, value);
    }

    /*
     * Filter section
     */
    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, bool>(nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddFilterCommandProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, ICommand?>(
            nameof(AddFilterCommand));

    public ICommand? AddFilterCommand
    {
        get => GetValue(AddFilterCommandProperty);
        set => SetValue(AddFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        AddFilterCommandParameterProperty =
            AvaloniaProperty.Register<ReturnChannelStrip, object?>(
                nameof(AddFilterCommandParameter));

    public object? AddFilterCommandParameter
    {
        get => GetValue(AddFilterCommandParameterProperty);
        set => SetValue(AddFilterCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?>
        DeleteFilterCommandProperty =
            AvaloniaProperty.Register<ReturnChannelStrip, ICommand?>(
                nameof(DeleteFilterCommand));

    public ICommand? DeleteFilterCommand
    {
        get => GetValue(DeleteFilterCommandProperty);
        set => SetValue(DeleteFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteFilterCommandParameterProperty =
            AvaloniaProperty.Register<ReturnChannelStrip, object?>(
                nameof(DeleteFilterCommandParameter));

    public object? DeleteFilterCommandParameter
    {
        get => GetValue(DeleteFilterCommandParameterProperty);
        set => SetValue(DeleteFilterCommandParameterProperty, value);
    }
    
    /*
     * Volume section
     */
    public static readonly StyledProperty<float> VolumeProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, float>(nameof(Volume));

    public float Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<float> PanProperty =
        AvaloniaProperty.Register<ReturnChannelStrip, float>(nameof(Pan));

    public float Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    /*
     * Audio to routing box
     */
    public static readonly StyledProperty<ObservableCollection<IRoutingTarget>?>
        AudioToRoutingTargetsProperty =
            AvaloniaProperty
                .Register<ReturnChannelStrip, ObservableCollection<IRoutingTarget>
                    ?>(nameof(AudioToRoutingTargets));

    public ObservableCollection<IRoutingTarget>? AudioToRoutingTargets
    {
        get => GetValue(AudioToRoutingTargetsProperty);
        set => SetValue(AudioToRoutingTargetsProperty, value);
    }

    public static readonly StyledProperty<IRoutingTarget?>
        SelectedAudioToRoutingTargetProperty =
            AvaloniaProperty.Register<ReturnChannelStrip, IRoutingTarget?>(
                nameof(SelectedAudioToRoutingTarget));

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get => GetValue(SelectedAudioToRoutingTargetProperty);
        set => SetValue(SelectedAudioToRoutingTargetProperty, value);
    }

    public ReturnChannelStrip()
    {
        InitializeComponent();
    }
}