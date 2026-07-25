using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.JackInputPortsViewModels;
using SonicEddy.Tools;

namespace SonicEddy.Views.JackInputPortsViews;

public partial class AddJackInputPortDialogView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public AddJackInputPortDialogView()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<AddJackInputPortDialogViewModel>()
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
            });
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
