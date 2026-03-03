using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SonicEddy.Controls.MixerControls;

public partial class MasterChannelStrip : UserControl
{
    /*
     * Channel header
     */
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<ChannelStrip, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<ChannelStrip, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        SelectCommandParameterProperty =
            AvaloniaProperty.Register<ChannelStrip, object?>(
                nameof(SelectCommandParameter));

    public object? SelectCommandParameter
    {
        get => GetValue(SelectCommandParameterProperty);
        set => SetValue(SelectCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<ChannelStrip, ICommand?>(
            nameof(DeleteCommand));

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteCommandParameterProperty =
            AvaloniaProperty.Register<ChannelStrip, object?>(
                nameof(DeleteCommandParameter));

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }
    
    /*
     * Filter section
     */
    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<ChannelStrip, bool>(
            nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddFilterCommandProperty =
        AvaloniaProperty.Register<ChannelStrip, ICommand?>(
            nameof(AddFilterCommand));

    public ICommand? AddFilterCommand
    {
        get => GetValue(AddFilterCommandProperty);
        set => SetValue(AddFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        AddFilterCommandParameterProperty =
            AvaloniaProperty.Register<ChannelStrip, object?>(
                nameof(AddFilterCommandParameter));

    public object? AddFilterCommandParameter
    {
        get => GetValue(AddFilterCommandParameterProperty);
        set => SetValue(AddFilterCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?>
        DeleteFilterCommandProperty =
            AvaloniaProperty.Register<ChannelStrip, ICommand?>(
                nameof(DeleteFilterCommand));

    public ICommand? DeleteFilterCommand
    {
        get => GetValue(DeleteFilterCommandProperty);
        set => SetValue(DeleteFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteFilterCommandParameterProperty =
            AvaloniaProperty.Register<ChannelStrip, object?>(
                nameof(DeleteFilterCommandParameter));

    public object? DeleteFilterCommandParameter
    {
        get => GetValue(DeleteFilterCommandParameterProperty);
        set => SetValue(DeleteFilterCommandParameterProperty, value);
    }

    /*
     * Parameter section
     */

    /*
     * First parameter section
     */
    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        FirstPluginParametersProperty =
            AvaloniaProperty
                .Register<ChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(FirstPluginParameters));

    public ObservableCollection<IParameter>? FirstPluginParameters
    {
        get => GetValue(FirstPluginParametersProperty);
        set => SetValue(FirstPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> FirstPluginTextProperty =
        AvaloniaProperty.Register<ChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string FirstPluginText
    {
        get => GetValue(FirstPluginTextProperty);
        set => SetValue(FirstPluginTextProperty, value);
    }

    /*
     * Second parameter section
     */
    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        SecondPluginParametersProperty =
            AvaloniaProperty
                .Register<ChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(SecondPluginParameters));

    public ObservableCollection<IParameter>? SecondPluginParameters
    {
        get => GetValue(SecondPluginParametersProperty);
        set => SetValue(SecondPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> SecondPluginTextProperty =
        AvaloniaProperty.Register<ChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string SecondPluginText
    {
        get => GetValue(SecondPluginTextProperty);
        set => SetValue(SecondPluginTextProperty, value);
    }

    /*
     * Third parameter section
     */
    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        ThirdPluginParametersProperty =
            AvaloniaProperty
                .Register<ChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(ThirdPluginParameters));

    public ObservableCollection<IParameter>? ThirdPluginParameters
    {
        get => GetValue(ThirdPluginParametersProperty);
        set => SetValue(ThirdPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> ThirdPluginTextProperty =
        AvaloniaProperty.Register<ChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string ThirdPluginText
    {
        get => GetValue(ThirdPluginTextProperty);
        set => SetValue(ThirdPluginTextProperty, value);
    }

    /*
     * Volume section
     */
    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<ChannelStrip, double>(
            nameof(Volume),
            defaultBindingMode: BindingMode.TwoWay);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<ChannelStrip, double>(
            nameof(Pan),
            defaultBindingMode: BindingMode.TwoWay);

    public double Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<MasterChannelStrip, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<MasterChannelStrip, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<MasterChannelStrip, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<MasterChannelStrip, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }

    /*
     * Audio to routing box
     */
    public static readonly StyledProperty<ObservableCollection<IRoutingTarget>?>
        AudioToRoutingTargetsProperty =
            AvaloniaProperty
                .Register<ChannelStrip, ObservableCollection<IRoutingTarget> ?>(
                    nameof(AudioToRoutingTargets));

    public ObservableCollection<IRoutingTarget>? AudioToRoutingTargets
    {
        get => GetValue(AudioToRoutingTargetsProperty);
        set => SetValue(AudioToRoutingTargetsProperty, value);
    }

    public static readonly StyledProperty<IRoutingTarget?>
        SelectedAudioToRoutingTargetProperty =
            AvaloniaProperty.Register<ChannelStrip, IRoutingTarget?>(
                nameof(SelectedAudioToRoutingTarget),
                defaultBindingMode: BindingMode.TwoWay);

    public IRoutingTarget? SelectedAudioToRoutingTarget
    {
        get => GetValue(SelectedAudioToRoutingTargetProperty);
        set => SetValue(SelectedAudioToRoutingTargetProperty, value);
    }

    public MasterChannelStrip()
    {
        InitializeComponent();
    }
}