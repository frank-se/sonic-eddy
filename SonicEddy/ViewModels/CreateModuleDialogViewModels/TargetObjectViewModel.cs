using ReactiveUI;

namespace SonicEddy.ViewModels.CreateModuleDialogViewModels;

public class TargetObjectViewModel : ReactiveObject
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ulong ObjectSerial { get; init; }
}