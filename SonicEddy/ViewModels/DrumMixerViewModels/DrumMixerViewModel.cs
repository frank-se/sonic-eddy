using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Fr.Sonic;
using Fr.Sonic.Model.Lv2;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.DrumMixer;

namespace SonicEddy.ViewModels.DrumMixerViewModels;

public sealed class DrumMixerViewModel : ViewModelBase, IDisposable
{
    private readonly IDrumMixerService _service;
    private readonly Dictionary<string, PluginDescription> _plugins;

    public DrumMixerViewModel(IDrumMixerService service)
    {
        _service = service;
        _plugins = FrSonicLv2.PluginDescriptions()
            .ToDictionary(plugin => plugin.Uri);

        SelectRoom = ReactiveCommand.Create(() => SelectReturn(true));
        SelectPlate = ReactiveCommand.Create(() => SelectReturn(false));

        for (var i = 0; i < 16; i++)
            Channels.Add(new(this, service, i));

        _service.Changed += OnServiceChanged;
        RefreshSources();
        foreach (var channel in Channels)
            channel.RefreshSource();
        SelectChannel(0);
    }

    public ObservableCollection<DrumMixerChannelViewModel> Channels { get; } =
        [];
    public ObservableCollection<DrumMixerSourceViewModel> Sources { get; } = [];
    public ReactiveCommand<Unit, Unit> SelectRoom { get; }
    public ReactiveCommand<Unit, Unit> SelectPlate { get; }

    public bool IsRoomSelected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsPlateSelected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float RoomVolume
    {
        get => _service.State.Room.Volume;
        set
        {
            _service.SetReturnVolume(true, value);
            this.RaisePropertyChanged();
        }
    }

    public float PlateVolume
    {
        get => _service.State.Plate.Volume;
        set
        {
            _service.SetReturnVolume(false, value);
            this.RaisePropertyChanged();
        }
    }

    public ObservableCollection<IParameter> Fil4Parameters
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<IParameter> TransientParameters
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<IParameter> CompressorParameters
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<IParameter> SaturatorParameters
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<IParameter> ReturnParameters
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsChannelParametersVisible
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsReturnParametersVisible
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    internal void SelectChannel(int index)
    {
        foreach (var channel in Channels)
            channel.IsSelected = channel.Index == index;
        IsRoomSelected = false;
        IsPlateSelected = false;
        IsChannelParametersVisible = true;
        IsReturnParametersVisible = false;

        Fil4Parameters = BuildChannelParameters(index, "fil4",
            DrumMixerGraphBuilder.Fil4Uri,
            ["enable", "HighPass", "HPfreq", "LowPass", "LPfreq",
                "LSfreq", "LSgain", "freq1", "q1", "gain1", "freq2", "q2",
                "gain2", "freq3", "q3", "gain3", "freq4", "q4", "gain4",
                "HSfreq", "HSgain"]);
        TransientParameters = BuildChannelParameters(index, "transient",
            DrumMixerGraphBuilder.TransientUri,
            ["bypass", "attack_time", "attack_boost", "release_time",
                "release_boost", "mix", "hipass", "lopass"]);
        CompressorParameters = BuildChannelParameters(index, "compressor",
            DrumMixerGraphBuilder.CompressorUri,
            ["bypass", "threshold", "ratio", "attack", "release", "makeup",
                "knee", "stereo_link", "mix"]);
        SaturatorParameters = BuildChannelParameters(index, "saturator",
            DrumMixerGraphBuilder.SaturatorUri,
            ["bypass", "drive", "blend", "mix", "post"]);
    }

    private void SelectReturn(bool room)
    {
        foreach (var channel in Channels)
            channel.IsSelected = false;
        IsRoomSelected = room;
        IsPlateSelected = !room;
        IsChannelParametersVisible = false;
        IsReturnParametersVisible = true;

        var uri = room
            ? DrumMixerGraphBuilder.RoomUri
            : DrumMixerGraphBuilder.PlateUri;
        var state = room ? _service.State.Room : _service.State.Plate;
        ReturnParameters = BuildParameters(uri,
            port => port.Symbol != "dry_level",
            parameter => state.Parameters.FirstOrDefault(value =>
                    value.Name == parameter.Symbol)?.Value ??
                parameter.Default ?? 0.0f,
            (parameter, value) =>
                _service.SetReturnParameter(room, parameter.Symbol, value));
    }

    private ObservableCollection<IParameter> BuildChannelParameters(int index,
        string prefix, string uri, IReadOnlyCollection<string> symbols)
    {
        var state = _service.State.Channels[index];
        return BuildParameters(uri, port => symbols.Contains(port.Symbol),
            parameter => state.Parameters.FirstOrDefault(value =>
                    value.Name == $"{prefix}:{parameter.Symbol}")?.Value ??
                parameter.Default ?? 0.0f,
            (parameter, value) => _service.SetParameter(index,
                $"{prefix}:{parameter.Symbol}", value));
    }

    private ObservableCollection<IParameter> BuildParameters(string uri,
        Func<PortDescription, bool> include,
        Func<PortDescription, float> initial,
        Action<PortDescription, float> changed)
    {
        if (!_plugins.TryGetValue(uri, out var plugin))
            return [];

        var result = new ObservableCollection<IParameter>();
        foreach (var port in plugin.Ports.Where(IsInputControl).Where(include))
        {
            var parameter = new DrumMixerParameterViewModel(port.Name,
                port.Symbol, port.Minimum ?? 0.0f, port.Maximum ?? 1.0f,
                initial(port), value => changed(port, value));
            parameter.EnableChanges();
            result.Add(parameter);
        }

        return result;
    }

    private static bool IsInputControl(PortDescription port) =>
        port.Classes.Any(value => value.EndsWith("#InputPort",
            StringComparison.Ordinal)) &&
        port.Classes.Any(value => value.EndsWith("#ControlPort",
            StringComparison.Ordinal));

    private void OnServiceChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var channel in Channels)
                channel.BeginLoad();
            RefreshSources();
            foreach (var channel in Channels)
                channel.RefreshSource();
        });
    }

    private void RefreshSources()
    {
        var ports = _service.GetSourcePorts();
        var existing = Sources.ToDictionary(source => source.Key);
        Sources.Clear();
        foreach (var port in ports)
        {
            var source = new DrumMixerSourceViewModel(port);
            Sources.Add(existing.GetValueOrDefault(source.Key) ?? source);
        }
    }

    public void Dispose()
    {
        _service.Changed -= OnServiceChanged;
        foreach (var channel in Channels)
            channel.Dispose();
    }
}

public sealed class DrumMixerChannelViewModel : ReactiveObject, IDisposable
{
    private readonly DrumMixerViewModel _owner;
    private readonly IDrumMixerService _service;
    private readonly CompositeDisposable _disposables = new();
    private bool _loading;
    private string _name;
    private bool _isSelected;
    private DrumMixerSourceViewModel? _selectedSource;

    public DrumMixerChannelViewModel(DrumMixerViewModel owner,
        IDrumMixerService service, int index)
    {
        _owner = owner;
        _service = service;
        Index = index;
        var state = service.State.Channels[index];
        _name = state.Name;
        _volume = state.Volume;
        _roomSend = state.RoomSend;
        _plateSend = state.PlateSend;
        Select = ReactiveCommand.Create(() => owner.SelectChannel(index));
        ClearSource = ReactiveCommand.Create(() =>
        {
            _loading = true;
            SelectedSource = null;
            _loading = false;
            _service.SetSource(Index, null);
        });
        Select.DisposeWith(_disposables);
        ClearSource.DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Name)
            .Skip(1)
            .Where(_ => !_loading)
            .Subscribe(v => _service.SetChannelName(Index, v))
            .DisposeWith(_disposables);
        this.WhenAnyValue(x => x.Volume)
            .Skip(1)
            .Where(_ => !_loading)
            .Subscribe(v => _service.SetChannelVolume(Index, v))
            .DisposeWith(_disposables);
        this.WhenAnyValue(x => x.RoomSend)
            .Skip(1)
            .Where(_ => !_loading)
            .Subscribe(v => _service.SetSend(Index, true, v))
            .DisposeWith(_disposables);
        this.WhenAnyValue(x => x.PlateSend)
            .Skip(1)
            .Where(_ => !_loading)
            .Subscribe(v => _service.SetSend(Index, false, v))
            .DisposeWith(_disposables);
        this.WhenAnyValue(x => x.SelectedSource)
            .Skip(1)
            .Where(_ => !_loading)
            .Where(v => v?.Port is not null)
            .Subscribe(v => _service.SetSource(Index, v!.Port))
            .DisposeWith(_disposables);

        RefreshSource();
    }

    public int Index { get; }
    public ReactiveCommand<Unit, Unit> Select { get; }
    public ReactiveCommand<Unit, Unit> ClearSource { get; }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private float _volume;
    public float Volume
    {
        get => _volume;
        set => this.RaiseAndSetIfChanged(ref _volume, value);
    }

    private float _roomSend;
    public float RoomSend
    {
        get => _roomSend;
        set => this.RaiseAndSetIfChanged(ref _roomSend, value);
    }

    private float _plateSend;
    public float PlateSend
    {
        get => _plateSend;
        set => this.RaiseAndSetIfChanged(ref _plateSend, value);
    }

    public DrumMixerSourceViewModel? SelectedSource
    {
        get => _selectedSource;
        set => this.RaiseAndSetIfChanged(ref _selectedSource, value);
    }

    internal void BeginLoad() => _loading = true;

    internal void RefreshSource()
    {
        _loading = true;
        var port = _service.ResolveSource(Index);
        if (port is not null)
        {
            SelectedSource = _owner.Sources.FirstOrDefault(source =>
                source.Port?.ObjectId == port.ObjectId);
        }
        else
        {
            var channel = _service.State.Channels[Index];
            SelectedSource = channel.SourceNodeName is not null ||
                             channel.SourcePortName is not null
                ? new DrumMixerSourceViewModel(channel.SourceNodeName,
                    channel.SourcePortName)
                : null;
        }
        _loading = false;
    }

    public void Dispose() => _disposables.Dispose();
}

public sealed class DrumMixerSourceViewModel
{
    private readonly Node? _node;

    public DrumMixerSourceViewModel(Port port)
    {
        Port = port;
        _node = FrSonic.NodeRegistry.GetByObjectId(port.Node.Id);
        NodeName = _node?.Name;
        PortName = port.Name ?? port.Alias ?? port.Channel;
    }

    public DrumMixerSourceViewModel(string? nodeName, string? portName)
    {
        NodeName = nodeName;
        PortName = portName;
    }

    public Port? Port { get; }
    public string? NodeName { get; }
    public string? PortName { get; }

    public string Name
    {
        get
        {
            var nodeName = _node?.Description ?? _node?.Name ?? NodeName ?? "Unknown";
            var portName = Port?.Alias ?? Port?.Name ?? Port?.Channel ?? PortName ?? "Unknown";
            return $"{nodeName}: {portName}";
        }
    }

    public string Key => $"{NodeName}:{PortName}";
}
