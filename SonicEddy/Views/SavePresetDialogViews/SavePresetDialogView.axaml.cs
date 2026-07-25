using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.SavePresetDialogViewModels;
using SonicEddy.Tools;

namespace SonicEddy.Views.SavePresetDialogViews;

public partial class SavePresetDialogView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public SavePresetDialogView()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<SavePresetDialogViewModel>()
            .Take(1)
            .Subscribe(vm =>
            {
                vm.Close.RegisterHandler(context =>
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

    public void Dispose() => _disposables.Dispose();
}
