using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.CreateModuleDialogViewModels;

namespace SonicEddy.Views.CreateModuleDialogViews;

public partial class CreateModuleDialogView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public CreateModuleDialogView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<CreateModuleDialogViewModel>()
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

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }

    public void Dispose() =>_disposables.Dispose();
}