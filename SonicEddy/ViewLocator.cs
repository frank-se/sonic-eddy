using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using SonicEddy.ViewModels;

namespace SonicEddy;

public class ViewLocator : IDataTemplate, IViewLocator
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType()
            .FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }

    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
    {
        var name = viewModel!.GetType()
            .FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
            return (IViewFor)Activator.CreateInstance(type)!;

        Console.WriteLine($"View not found: {name}");
        return null;
    }
}