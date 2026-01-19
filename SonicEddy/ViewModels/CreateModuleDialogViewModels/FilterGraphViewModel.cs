using System;
using ReactiveUI;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public class FilterGraphViewModel : ReactiveObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
}