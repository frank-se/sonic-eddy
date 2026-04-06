using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveUI;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphParameterDescriptionViewModel : ReactiveObject,
    IDisposable
{
    private CompositeDisposable _disposables = new();

    public FilterGraphParameterDescriptionViewModel(string name,
        string displayName, float min, float max, float @default, int sortOrder,
        Action changeNotification)
    {
        Name = name;
        Min = min;
        Max = max;
        Default = @default;
        SortOrder = sortOrder;
        DisplayName = displayName;

        this.WhenAnyValue(x => x.SortOrder)
            .Skip(1)
            .Subscribe(_ => changeNotification())
            .DisposeWith(_disposables);
    }
    
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string DisplayName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Min
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Max
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public float Default
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int SortOrder
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}