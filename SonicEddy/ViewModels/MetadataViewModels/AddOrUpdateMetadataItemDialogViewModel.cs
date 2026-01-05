using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace SonicEddy.ViewModels.MetadataViewModels;

public class AddOrUpdateMetadataItemDialogViewModel : ViewModelBase, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private ulong _subject = 0;
    private string _key = string.Empty;
    private string _type = string.Empty;
    private string _value = string.Empty;
    private bool _isButtonEnabled = false;

    public bool DialogResult = false;
    public required bool IsAddMode { get; init; }

    public AddOrUpdateMetadataItemDialogViewModel()
    {
        this.WhenAnyValue(x => x.Key)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Type)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);

        this.WhenAnyValue(x => x.Value)
            .Subscribe(_ => ValidateForm())
            .DisposeWith(_disposables);
    }
    
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }

    public ulong Subject
    {
        get => _subject;
        set => this.RaiseAndSetIfChanged(ref _subject, value);
    }

    public string Key
    {
        get => _key;
        set => this.RaiseAndSetIfChanged(ref _key, value);
    }

    public string Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsButtonEnabled
    {
        get => _isButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref _isButtonEnabled, value);
    }

    private void ValidateForm()
    {
        IsButtonEnabled = Key != string.Empty && Type != string.Empty &&
                          Value != string.Empty;
    }

    public Interaction<Unit, Unit> Close { get; } = new();

    public async Task CancelAction()
    {
        DialogResult = false;
        await Close.Handle(Unit.Default);
    }

    public async Task AddMetadataAction()
    {
        DialogResult = true;
        await Close.Handle(Unit.Default);
    }
}