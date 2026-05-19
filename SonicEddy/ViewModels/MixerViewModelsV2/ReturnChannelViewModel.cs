using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Windows.Input;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Controls.MixerControls;
using SonicEddy.Services.Monitoring;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public class ReturnChannelViewModel : ReactiveObject, IReturnChannel,
    IDisposable
{
    private CompositeDisposable? _disposable = new();

    private readonly LoopbackModule _inputLoopback;
    private readonly LoopbackModule _outbackLoopback;

    public LoopbackModule InputLoopback => _inputLoopback;
    public LoopbackModule OutputLoopback => _outbackLoopback;

    public ReturnChannelViewModel(string text, ICommand selectChannelCommand,
        LoopbackModule inputLoopback, LoopbackModule outbackLoopback,
        FilterChain? filterChain, IMonitoringService monitoringService)
    {
        Text = text;
        SelectChannelCommand = selectChannelCommand;
        _inputLoopback = inputLoopback;
        _outbackLoopback = outbackLoopback;

        this.WhenAnyValue(x => x.FilterChain)
            .Subscribe(chain =>
            {
                if (chain is null)
                {
                    HasFilter = false;
                    Parameters = [];
                    return;
                }

                // ReSharper disable once MergeIntoPattern
                // DO NOT CHANGE TO PATTERN
                // OTHERWISE ACCESS TO RESULT MIGHT BE BLOCKING!
                if (!chain.CaptureNode.Params.IsCompleted ||
                    chain.CaptureNode.Params.Result is null ||
                    !chain.CaptureNode.PropertyInfos.IsCompleted)
                    return;

                Parameters = [];

                HasFilter = true;
            })
            .DisposeWith(_disposable!);

        PanAndVolume = new PanAndVolumeViewModel(outbackLoopback.PlaybackNode);

        FilterChain = filterChain;
    }

    public FilterChain? FilterChain
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Text { get; }

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<ParameterCollection>? Parameters
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasFilter
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public IPanAndVolume PanAndVolume { get; }

    public ICommand SelectChannelCommand { get; set; }

    public void OnSelectChannel() =>
        SelectChannelCommand.Execute(this);

    public void OnAddFilter(IOutputChannel channel)
    {
        throw new System.NotImplementedException();
    }

    public void OnDeleteFilter(IOutputChannel channel)
    {
        throw new System.NotImplementedException();
    }

    public void Dispose()
    {
        if (PanAndVolume is PanAndVolumeViewModel panAndVolume)
            panAndVolume.Dispose();

        _disposable?.Dispose();
        _disposable = null;

        GC.SuppressFinalize(this);
    }
}