using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace SonicEddy.Controls.MixerControls;

public partial class SpecialChannelsSection : UserControl
{
    public static readonly StyledProperty<ObservableCollection<IOutputChannel>?>
        OutputChannelsProperty =
            AvaloniaProperty
                .Register<SpecialChannelsSection,
                    ObservableCollection<IOutputChannel>?>(
                    nameof(OutputChannels));

    public ObservableCollection<IOutputChannel>? OutputChannels
    {
        get => GetValue(OutputChannelsProperty);
        set => SetValue(OutputChannelsProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IReturnChannel>?>
        ReturnChannelsProperty =
            AvaloniaProperty
                .Register<SpecialChannelsSection,
                    ObservableCollection<IReturnChannel>?>(
                    nameof(ReturnChannels));

    public ObservableCollection<IReturnChannel>? ReturnChannels
    {
        get => GetValue(ReturnChannelsProperty);
        set => SetValue(ReturnChannelsProperty, value);
    }

    public static readonly StyledProperty<ObservableCollection<IInputChannel>?>
        InputChannelsProperty =
            AvaloniaProperty
                .Register<SpecialChannelsSection,
                    ObservableCollection<IInputChannel>?>(
                    nameof(InputChannels));

    public ObservableCollection<IInputChannel>? InputChannels
    {
        get => GetValue(InputChannelsProperty);
        set => SetValue(InputChannelsProperty, value);
    }

    public SpecialChannelsSection()
    {
        InitializeComponent();
    }
}