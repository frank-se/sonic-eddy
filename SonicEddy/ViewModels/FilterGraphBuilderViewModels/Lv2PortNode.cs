using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Fr.Lv2.Model;

namespace SonicEddy.ViewModels.FilterGraphBuilderViewModels;

public class Lv2PortNode(PortDescription description, NodeBase node)
    : PortNodeBase(description.Name, node)
{
    public PortDescription Description { get; } = description;
}