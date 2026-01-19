using System;
using ReactiveUI;
using SonicEddy.Services.AppData;

namespace SonicEddy.ViewModels.FilterGraphManagerViewModels;

public class FilterGraphViewModel : ReactiveObject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int NumberOfNodes { get; init; }
    public int NumberOfEdges { get; init; }
}