using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using SonicEddy.Contracts.ExternalEffects;
using SonicEddy.Services.ExternalEffects;
using SonicEddy.Services.Wireplumber;
using SonicEddy.Tools;
using SonicEddy.Views.ExternalEffectsViews;

namespace SonicEddy.ViewModels.ExternalEffectsViewModels;

public sealed class ExternalEffectsViewModel : ViewModelBase, IDisposable
{
    private readonly IExternalEffectService _service;
    private readonly IWireplumberService _wireplumber;

    public ExternalEffectsViewModel(IExternalEffectService service,
        IWireplumberService wireplumber)
    {
        _service = service;
        _wireplumber = wireplumber;
        _service.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<ExternalEffectRowViewModel> Effects { get; } =
        [];

    public ExternalEffectRowViewModel? SelectedEffect
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanEditOrDelete));
        }
    }

    public bool CanEditOrDelete => SelectedEffect is { IsUsed: false };

    public async Task Create()
    {
        var editor = new ExternalEffectEditorViewModel(_wireplumber);
        await ShowEditor(editor);
        if (!editor.DialogResult) return;
        await _service.AddAsync(editor.Name, editor.SelectedInputNode!,
            [editor.SelectedInputLeft!, editor.SelectedInputRight!],
            editor.SelectedOutputNode!,
            [editor.SelectedOutputLeft!, editor.SelectedOutputRight!]);
    }

    public async Task Edit()
    {
        if (SelectedEffect is null || SelectedEffect.IsUsed) return;
        var editor = new ExternalEffectEditorViewModel(_wireplumber,
            SelectedEffect.Config);
        await ShowEditor(editor);
        if (!editor.DialogResult) return;
        await _service.UpdateAsync(SelectedEffect.Config.Id, editor.Name,
            editor.SelectedInputNode!,
            [editor.SelectedInputLeft!, editor.SelectedInputRight!],
            editor.SelectedOutputNode!,
            [editor.SelectedOutputLeft!, editor.SelectedOutputRight!]);
    }

    public async Task Delete()
    {
        if (SelectedEffect is null || SelectedEffect.IsUsed) return;
        await _service.DeleteAsync(SelectedEffect.Config.Id);
    }

    private static async Task ShowEditor(ExternalEffectEditorViewModel editor)
    {
        var window = new ExternalEffectEditorWindow { DataContext = editor };
        await window.ShowDialog(WindowTools.GetMainWindow()!);
    }

    private void Refresh()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var selectedId = SelectedEffect?.Config.Id;
            Effects.Clear();
            foreach (var effect in _service.Effects.OrderBy(effect => effect.Name))
                Effects.Add(new(effect, _service.IsAvailable(effect.Id),
                    _service.GetUsedBy(effect.Id)));
            SelectedEffect = Effects.FirstOrDefault(effect =>
                effect.Config.Id == selectedId);
        });
    }

    public void Dispose()
    {
        _service.Changed -= Refresh;
        GC.SuppressFinalize(this);
    }
}

public sealed class ExternalEffectRowViewModel(
    ExternalEffectConfig config,
    bool isAvailable,
    string? usedBy)
{
    public ExternalEffectConfig Config { get; } = config;
    public string Name => Config.Name;
    public string Availability => isAvailable ? "Available" : "Unavailable";
    public string UsedBy => usedBy ?? string.Empty;
    public bool IsUsed => usedBy is not null;
}
