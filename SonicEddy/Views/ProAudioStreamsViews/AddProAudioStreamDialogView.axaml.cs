using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Controls;
using ReactiveUI;
using SonicEddy.ViewModels.ProAudioStreamsViewModels;

namespace SonicEddy.Views.ProAudioStreamsViews;

public partial class AddProAudioStreamDialogView : Window, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public AddProAudioStreamDialogView()
    {
        InitializeComponent();

        this.WhenAnyValue(x => x.DataContext)!
            .OfType<AddProAudioStreamDialogViewModel>()
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

    public void Dispose()
    {
        _disposables.Dispose();
    }
}