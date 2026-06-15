using System;
using System.Windows.Input;
using Avalonia.Controls;
using Fr.Sonic.Modules.Models;
using ReactiveUI;
using SonicEddy.Tools;
using SonicEddy.Views.MixerViewsV2;

namespace SonicEddy.ViewModels.MixerViewModelsV2;

public sealed class DuckerSectionViewModel : ReactiveObject, IDisposable
{
    private readonly Ducker _ducker;
    private Window? _detailsWindow;

    public DuckerSectionViewModel(Ducker ducker)
    {
        _ducker = ducker;
        ShowDetailsCommand = ReactiveCommand.Create(ShowDetails);
        ApplyParams();
    }

    public ICommand ShowDetailsCommand { get; }

    public bool Bypass
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetParam("bypass", value ? 1.0f : 0.0f);
        }
    }

    public double Attack
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetParam("attack", (float)value / 1000f);
        }
    } = 50.0;

    public double Hold
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetParam("hold", (float)value / 1000f);
        }
    } = 100.0;

    public double Release
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetParam("release", (float)value / 1000f);
        }
    } = 150.0;

    public double Depth
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetParam("depth", (float)value);
        }
    } = 1.0;

    public int AttackShape
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetIntParam("attack_shape", value);
        }
    }

    public int ReleaseShape
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetIntParam("release_shape", value);
        }
    }

    public int MidiChannel
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            SetIntParam("midi_channel", value);
        }
    } // 0-based; 0 = channel 1

    public string[] ShapeOptions { get; } = ["Linear", "Exponential", "Logarithmic"];
    public string[] MidiChannelOptions { get; } =
        ["1", "2", "3", "4", "5", "6", "7", "8",
         "9", "10", "11", "12", "13", "14", "15", "16"];

    private void ApplyParams()
    {
        SetParam("attack", (float)Attack / 1000f);
        SetParam("hold", (float)Hold / 1000f);
        SetParam("release", (float)Release / 1000f);
        SetParam("depth", (float)Depth);
        SetIntParam("attack_shape", AttackShape);
        SetIntParam("release_shape", ReleaseShape);
        SetParam("bypass", Bypass ? 1.0f : 0.0f);
        SetIntParam("midi_channel", MidiChannel);
    }

    private void SetParam(string key, float value) =>
        _ducker.AudioCaptureNode.SetParam(key, value);

    private void SetIntParam(string key, int value) =>
        _ducker.AudioCaptureNode.SetParam(key, value);

    private void ShowDetails()
    {
        if (_detailsWindow is not null)
        {
            _detailsWindow.Activate();
            return;
        }

        _detailsWindow = new DuckerDetailsWindow { DataContext = this };
        _detailsWindow.Closed += OnDetailsWindowClosed;
        var owner = WindowTools.GetMainWindow();
        if (owner is null)
            _detailsWindow.Show();
        else
            _detailsWindow.Show(owner);
    }

    private void OnDetailsWindowClosed(object? sender, EventArgs e)
    {
        if (_detailsWindow is not null)
            _detailsWindow.Closed -= OnDetailsWindowClosed;
        _detailsWindow = null;
    }

    public void Dispose()
    {
        if (_detailsWindow is not null)
            _detailsWindow.Close();
    }
}
