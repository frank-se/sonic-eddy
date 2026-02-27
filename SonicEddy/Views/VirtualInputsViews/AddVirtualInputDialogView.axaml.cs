using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.VirtualInputsViewModels;

namespace SonicEddy.Views.VirtualInputsViews;

public partial class AddVirtualInputDialogView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public AddVirtualInputDialogView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<AddVirtualInputDialogViewModel>()
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