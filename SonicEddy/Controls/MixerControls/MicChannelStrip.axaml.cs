using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.Controls.MixerControls;

public partial class MicChannelStrip : UserControl
{
    /*
     * Channel header
     */
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MicChannelStrip, string>(
            nameof(Text),
            defaultValue: string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<MicChannelStrip, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<MicChannelStrip, ICommand?>(
            nameof(SelectCommand));

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        SelectCommandParameterProperty =
            AvaloniaProperty.Register<MicChannelStrip, object?>(
                nameof(SelectCommandParameter));

    public object? SelectCommandParameter
    {
        get => GetValue(SelectCommandParameterProperty);
        set => SetValue(SelectCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<MicChannelStrip, ICommand?>(
            nameof(DeleteCommand));

    public ICommand? DeleteCommand
    {
        get => GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteCommandParameterProperty =
            AvaloniaProperty.Register<MicChannelStrip, object?>(
                nameof(DeleteCommandParameter));

    public object? DeleteCommandParameter
    {
        get => GetValue(DeleteCommandParameterProperty);
        set => SetValue(DeleteCommandParameterProperty, value);
    }

    /*
     * Audio from routing box
     */
    public static readonly StyledProperty<ObservableCollection<IRoutingTarget>?>
        AudioFromRoutingTargetsProperty =
            AvaloniaProperty
                .Register<MicChannelStrip, ObservableCollection<IRoutingTarget>
                    ?>(nameof(AudioFromRoutingTargets));

    public ObservableCollection<IRoutingTarget>? AudioFromRoutingTargets
    {
        get => GetValue(AudioFromRoutingTargetsProperty);
        set => SetValue(AudioFromRoutingTargetsProperty, value);
    }

    public static readonly StyledProperty<IRoutingTarget?>
        SelectedAudioFromRoutingTargetProperty =
            AvaloniaProperty.Register<MicChannelStrip, IRoutingTarget?>(
                nameof(SelectedAudioFromRoutingTarget),
                defaultBindingMode: BindingMode.TwoWay);

    public IRoutingTarget? SelectedAudioFromRoutingTarget
    {
        get => GetValue(SelectedAudioFromRoutingTargetProperty);
        set => SetValue(SelectedAudioFromRoutingTargetProperty, value);
    }

    /*
     * Filter section
     */
    public static readonly StyledProperty<bool> HasFilterProperty =
        AvaloniaProperty.Register<MicChannelStrip, bool>(
            nameof(HasFilter));

    public bool HasFilter
    {
        get => GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public static readonly StyledProperty<ICommand?> AddFilterCommandProperty =
        AvaloniaProperty.Register<MicChannelStrip, ICommand?>(
            nameof(AddFilterCommand));

    public ICommand? AddFilterCommand
    {
        get => GetValue(AddFilterCommandProperty);
        set => SetValue(AddFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        AddFilterCommandParameterProperty =
            AvaloniaProperty.Register<MicChannelStrip, object?>(
                nameof(AddFilterCommandParameter));

    public object? AddFilterCommandParameter
    {
        get => GetValue(AddFilterCommandParameterProperty);
        set => SetValue(AddFilterCommandParameterProperty, value);
    }

    public static readonly StyledProperty<ICommand?>
        DeleteFilterCommandProperty =
            AvaloniaProperty.Register<MicChannelStrip, ICommand?>(
                nameof(DeleteFilterCommand));

    public ICommand? DeleteFilterCommand
    {
        get => GetValue(DeleteFilterCommandProperty);
        set => SetValue(DeleteFilterCommandProperty, value);
    }

    public static readonly StyledProperty<object?>
        DeleteFilterCommandParameterProperty =
            AvaloniaProperty.Register<MicChannelStrip, object?>(
                nameof(DeleteFilterCommandParameter));

    public object? DeleteFilterCommandParameter
    {
        get => GetValue(DeleteFilterCommandParameterProperty);
        set => SetValue(DeleteFilterCommandParameterProperty, value);
    }

    public static readonly StyledProperty<IList<FilterChainPresetViewModel>?>
        PresetsProperty =
            AvaloniaProperty.Register<MicChannelStrip, IList<FilterChainPresetViewModel>?>(
                nameof(Presets));

    public IList<FilterChainPresetViewModel>? Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public static readonly StyledProperty<ICommand?> SavePresetCommandProperty =
        AvaloniaProperty.Register<MicChannelStrip, ICommand?>(nameof(SavePresetCommand));

    public ICommand? SavePresetCommand
    {
        get => GetValue(SavePresetCommandProperty);
        set => SetValue(SavePresetCommandProperty, value);
    }

    public static readonly StyledProperty<ICommand?> LoadPresetCommandProperty =
        AvaloniaProperty.Register<MicChannelStrip, ICommand?>(nameof(LoadPresetCommand));

    public ICommand? LoadPresetCommand
    {
        get => GetValue(LoadPresetCommandProperty);
        set => SetValue(LoadPresetCommandProperty, value);
    }

    /*
     * First parameter section
     */
    public static readonly StyledProperty<ObservableCollection<IParameter>?>
        FirstPluginParametersProperty =
            AvaloniaProperty
                .Register<MicChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(FirstPluginParameters));

    public ObservableCollection<IParameter>? FirstPluginParameters
    {
        get => GetValue(FirstPluginParametersProperty);
        set => SetValue(FirstPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> FirstPluginTextProperty =
        AvaloniaProperty.Register<MicChannelStrip, string>(
            nameof(FirstPluginText),
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
                .Register<MicChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(SecondPluginParameters));

    public ObservableCollection<IParameter>? SecondPluginParameters
    {
        get => GetValue(SecondPluginParametersProperty);
        set => SetValue(SecondPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> SecondPluginTextProperty =
        AvaloniaProperty.Register<MicChannelStrip, string>(
            nameof(SecondPluginText),
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
                .Register<MicChannelStrip, ObservableCollection<IParameter>?>(
                    nameof(ThirdPluginParameters));

    public ObservableCollection<IParameter>? ThirdPluginParameters
    {
        get => GetValue(ThirdPluginParametersProperty);
        set => SetValue(ThirdPluginParametersProperty, value);
    }

    public static readonly StyledProperty<string> ThirdPluginTextProperty =
        AvaloniaProperty.Register<MicChannelStrip, string>(
            nameof(ThirdPluginText),
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
        AvaloniaProperty.Register<MicChannelStrip, double>(
            nameof(Volume),
            defaultBindingMode: BindingMode.TwoWay);

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public static readonly StyledProperty<double> PanProperty =
        AvaloniaProperty.Register<MicChannelStrip, double>(
            nameof(Pan),
            defaultBindingMode: BindingMode.TwoWay);

    public double Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<MicChannelStrip, bool>(
            nameof(IsMuted),
            defaultBindingMode: BindingMode.TwoWay);

    public bool IsMuted
    {
        get => GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    public static readonly StyledProperty<float> LeftAverageProperty =
        AvaloniaProperty.Register<MicChannelStrip, float>(
            nameof(LeftAverage));

    public float LeftAverage
    {
        get => GetValue(LeftAverageProperty);
        set => SetValue(LeftAverageProperty, value);
    }

    public static readonly StyledProperty<float> RightAverageProperty =
        AvaloniaProperty.Register<MicChannelStrip, float>(
            nameof(RightAverage));

    public float RightAverage
    {
        get => GetValue(RightAverageProperty);
        set => SetValue(RightAverageProperty, value);
    }

    public static readonly StyledProperty<float> LeftPeakProperty =
        AvaloniaProperty.Register<MicChannelStrip, float>(
            nameof(LeftPeak));

    public float LeftPeak
    {
        get => GetValue(LeftPeakProperty);
        set => SetValue(LeftPeakProperty, value);
    }

    public static readonly StyledProperty<float> RightPeakProperty =
        AvaloniaProperty.Register<MicChannelStrip, float>(
            nameof(RightPeak));

    public float RightPeak
    {
        get => GetValue(RightPeakProperty);
        set => SetValue(RightPeakProperty, value);
    }

    /*
     * Midi control state
     */
    public static readonly StyledProperty<bool> IsFilterMidiControlledProperty =
        AvaloniaProperty.Register<MicChannelStrip, bool>(
            nameof(IsFilterMidiControlled));

    public bool IsFilterMidiControlled
    {
        get => GetValue(IsFilterMidiControlledProperty);
        set => SetValue(IsFilterMidiControlledProperty, value);
    }

    public static readonly StyledProperty<bool>
        FirstPluginParametersMidiControlledProperty =
            AvaloniaProperty.Register<MicChannelStrip, bool>(
                nameof(FirstPluginParametersMidiControlled));

    public bool FirstPluginParametersMidiControlled
    {
        get => GetValue(FirstPluginParametersMidiControlledProperty);
        set => SetValue(FirstPluginParametersMidiControlledProperty, value);
    }

    public static readonly StyledProperty<bool>
        SecondPluginParametersMidiControlledProperty =
            AvaloniaProperty.Register<MicChannelStrip, bool>(
                nameof(SecondPluginParametersMidiControlled));

    public bool SecondPluginParametersMidiControlled
    {
        get => GetValue(SecondPluginParametersMidiControlledProperty);
        set => SetValue(SecondPluginParametersMidiControlledProperty, value);
    }

    public static readonly StyledProperty<bool>
        ThirdPluginParametersMidiControlledProperty =
            AvaloniaProperty.Register<MicChannelStrip, bool>(
                nameof(ThirdPluginParametersMidiControlled));

    public bool ThirdPluginParametersMidiControlled
    {
        get => GetValue(ThirdPluginParametersMidiControlledProperty);
        set => SetValue(ThirdPluginParametersMidiControlledProperty, value);
    }

    public MicChannelStrip()
    {
        InitializeComponent();
    }
}
