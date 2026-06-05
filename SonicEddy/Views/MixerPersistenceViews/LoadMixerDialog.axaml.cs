using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.MixerPersistenceViewModels;

namespace SonicEddy.Views.MixerPersistenceViews;

public partial class LoadMixerDialog : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public LoadMixerDialog()
    {
        InitializeComponent();
        this.WhenAnyValue(view => view.DataContext)!
            .OfType<LoadMixerDialogViewModel>()
            .Take(1)
            .Subscribe(viewModel =>
                viewModel.Close.RegisterHandler(context =>
                    {
                        Close();
                        context.SetOutput(Unit.Default);
                        return Observable.Return(Unit.Default);
                    })
                    .DisposeWith(_disposables));
    }

    public void Dispose() => _disposables.Dispose();
}
