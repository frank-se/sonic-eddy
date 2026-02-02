using Avalonia.Controls;
using SonicEddy.Controls.GraphEditorControl;

namespace SonicEddy.ViewModels.GraphControlTesterViewModels;

public class TestPortViewModel(string name) : IGraphPort
{
    public string Name { get; } = name;
    public Control? Control { get; set; }
}