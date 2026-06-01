using System;
using System.Collections.Generic;
using Fr.Sonic.Model.Objects;
using Fr.Sonic.Model.Params;
using ReactiveUI;
using SonicEddy.Services.MixerServiceV2;
using SonicEddy.ViewModels.MixerViewModelsV2;

namespace SonicEddy.ViewModels.GlobalMasterViewModels;

public class GlobalMasterViewModel : ReactiveObject, IDisposable
{
    private readonly Node _xfadeNode;
    private readonly Node? _cueNode;
    private bool _internal;

    public GlobalMasterViewModel(GlobalMasterChannel globalMaster,
        MasterChannelViewModel layerA, MasterChannelViewModel layerB,
        CueChannel? cue)
    {
        LayerA = layerA;
        LayerB = layerB;
        HasCue = cue is not null;

        _xfadeNode = globalMaster.CrossFader.CaptureNode;
        _xfadeNode.ParamsChanged += OnXfadeParamsChanged;

        if (cue is not null)
        {
            _cueNode = cue.CrossFader.CaptureNode;
            _cueNode.ParamsChanged += OnCueParamsChanged;
            CueChannel = new CueChannelViewModel(cue, layerA.AudioToRoutingTargets);
        }
    }

    private void OnXfadeParamsChanged(
        Dictionary<string, Fr.Sonic.Model.Params.IParameter>? parameters)
    {
        if (parameters is null) return;
        _internal = true;
        if (parameters.TryGetValue("xfade", out var xp) && xp is Parameter<float> xf)
            Xfade = xf.Value;
        if (parameters.TryGetValue("shape", out var sp) && sp is Parameter<float> sf)
            ShapeIndex = sf.Value > 0.5f ? 1 : 0;
        if (parameters.TryGetValue("mode", out var mp) && mp is Parameter<float> mf)
            ModeIndex = mf.Value > 0.5f ? 1 : 0;
        _internal = false;
    }

    private void OnCueParamsChanged(
        Dictionary<string, Fr.Sonic.Model.Params.IParameter>? parameters)
    {
        if (parameters is null) return;
        _internal = true;
        if (parameters.TryGetValue("xfade", out var xp) && xp is Parameter<float> xf)
            CueXfade = xf.Value;
        if (parameters.TryGetValue("shape", out var sp) && sp is Parameter<float> sf)
            CueShapeIndex = sf.Value > 0.5f ? 1 : 0;
        if (parameters.TryGetValue("mode", out var mp) && mp is Parameter<float> mf)
            CueModeIndex = mf.Value > 0.5f ? 1 : 0;
        _internal = false;
    }

    public MasterChannelViewModel LayerA { get; }
    public MasterChannelViewModel LayerB { get; }
    public CueChannelViewModel? CueChannel { get; }
    public bool HasCue { get; }

    // Main xfade
    private double _xfade;
    public double Xfade
    {
        get => _xfade;
        set
        {
            this.RaiseAndSetIfChanged(ref _xfade, value);
            if (!_internal) _xfadeNode.SetParam("xfade", (float)value);
        }
    }

    private int _shapeIndex;
    public int ShapeIndex
    {
        get => _shapeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _shapeIndex, value);
            if (!_internal) _xfadeNode.SetParam("shape", (float)value);
        }
    }

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _modeIndex, value);
            if (!_internal) _xfadeNode.SetParam("mode", (float)value);
        }
    }

    // Cue xfade
    private double _cueXfade;
    public double CueXfade
    {
        get => _cueXfade;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueXfade, value);
            if (!_internal) _cueNode?.SetParam("xfade", (float)value);
        }
    }

    private int _cueShapeIndex;
    public int CueShapeIndex
    {
        get => _cueShapeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueShapeIndex, value);
            if (!_internal) _cueNode?.SetParam("shape", (float)value);
        }
    }

    private int _cueModeIndex;
    public int CueModeIndex
    {
        get => _cueModeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _cueModeIndex, value);
            if (!_internal) _cueNode?.SetParam("mode", (float)value);
        }
    }

    public void Dispose()
    {
        _xfadeNode.ParamsChanged -= OnXfadeParamsChanged;
        if (_cueNode is not null) _cueNode.ParamsChanged -= OnCueParamsChanged;
        CueChannel?.Dispose();
        GC.SuppressFinalize(this);
    }
}
