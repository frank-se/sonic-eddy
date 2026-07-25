using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using SonicEddy.ViewModels.MetadataViewModels;
using SonicEddy.Tools;

namespace SonicEddy.Views.MetadataViews;

public partial class AddOrUpdateMetadataItemDialogView : Window
{
    private readonly CompositeDisposable _disposables = new();

    public AddOrUpdateMetadataItemDialogView()
    {
        InitializeComponent();
        WaylandAppId.Apply(this, "sonic-eddy-utils");

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<AddOrUpdateMetadataItemDialogViewModel>()
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
}