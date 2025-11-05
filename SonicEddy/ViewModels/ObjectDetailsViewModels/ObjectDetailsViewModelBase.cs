namespace SonicEddy.ViewModels.ObjectDetailsViewModels;

public class ObjectDetailsViewModelBase(
    ulong objectId,
    ulong objectSerial,
    string type)
    : ViewModelBase
{
    public ulong ObjectId => objectId;
    public ulong ObjectSerial => objectSerial;
    public string Type => type;
    public string Title => $"{Type} - {ObjectSerial}";
}