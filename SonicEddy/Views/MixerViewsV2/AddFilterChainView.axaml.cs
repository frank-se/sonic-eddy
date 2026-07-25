using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.MixerViewModelsV2;
using SonicEddy.Tools;

namespace SonicEddy.Views.MixerViewsV2;

public partial class AddFilterChainView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public AddFilterChainView()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<AddFilterChainViewModel>()
            .Take(1)
            .Subscribe(viewModel =>
            {
                viewModel.Close.RegisterHandler(context =>
                    {
                        Close();
                        context.SetOutput(Unit.Default);
                        return Observable.Return(Unit.Default);
                    })
                    .DisposeWith(_disposables);
            })
            .DisposeWith(_disposables);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }

    public void Dispose() => _disposables.Dispose();
}