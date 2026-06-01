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
    private readonly Node _node;
    private bool _internal;

    public GlobalMasterViewModel(GlobalMasterChannel globalMaster,
        MasterChannelViewModel layerA, MasterChannelViewModel layerB)
    {
        LayerA = layerA;
        LayerB = layerB;
        _node = globalMaster.CrossFader.CaptureNode;
        _node.ParamsChanged += OnParamsChanged;
    }

    private void OnParamsChanged(
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

    public MasterChannelViewModel LayerA { get; }
    public MasterChannelViewModel LayerB { get; }

    private double _xfade;
    public double Xfade
    {
        get => _xfade;
        set
        {
            this.RaiseAndSetIfChanged(ref _xfade, value);
            if (!_internal) _node.SetParam("xfade", (float)value);
        }
    }

    private int _shapeIndex;
    public int ShapeIndex
    {
        get => _shapeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _shapeIndex, value);
            if (!_internal) _node.SetParam("shape", (float)value);
        }
    }

    private int _modeIndex;
    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            this.RaiseAndSetIfChanged(ref _modeIndex, value);
            if (!_internal) _node.SetParam("mode", (float)value);
        }
    }

    public void Dispose()
    {
        _node.ParamsChanged -= OnParamsChanged;
        GC.SuppressFinalize(this);
    }
}
