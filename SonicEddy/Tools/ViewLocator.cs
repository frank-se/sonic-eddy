using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SonicEddy.ViewModels;
using SonicEddy.Views;

namespace SonicEddy.Tools;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        return param switch
        {
            MixerViewModel => new MixerView(),
            ObjectBrowserViewModel => new ObjectBrowserView(),
            _ => null
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}