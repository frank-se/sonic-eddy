using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.ExternalEffectsViewModels;

namespace SonicEddy.Views.ExternalEffectsViews;

public partial class ExternalEffectEditorWindow : Window
{
    private readonly CompositeDisposable _disposables = new();

    public ExternalEffectEditorWindow()
    {
        InitializeComponent();
        this.WhenAnyValue(window => window.DataContext)!
            .OfType<ExternalEffectEditorViewModel>().Take(1)
            .Subscribe(viewModel => viewModel.Close.RegisterHandler(context =>
            {
                Close();
                context.SetOutput(Unit.Default);
                return Observable.Return(Unit.Default);
            }).DisposeWith(_disposables)).DisposeWith(_disposables);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }
}
