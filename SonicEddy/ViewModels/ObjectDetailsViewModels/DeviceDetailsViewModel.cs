using Fr.Sonic.Model;
using Fr.Sonic.Model.Objects;
using ReactiveUI;

namespace SonicEddy.ViewModels.ObjectDetailsViewModels;

public class DeviceDetailsViewModel(
    ulong objectId,
    ulong objectSerial,
    string deviceClass,
    string description,
    string formFactor,
    string icon,
    string iconName,
    string intendedRoles,
    string name,
    string nick,
    string profile,
    string profileSet,
    string serial,
    string subSystem,
    bool autoPort,
    bool autoProfile,
    ulong? proChannels,
    ulong? probeRate,
    bool disableMixerPath,
    bool ignoreDb,
    string path,
    bool softMixer,
    bool splitEnable,
    bool useUcm,
    string productId,
    string productName,
    string vendorId,
    string vendorName)
    : ObjectDetailsViewModelBase(objectId, objectSerial, "Device"),
        IActivatableViewModel
{
    public string DeviceClass => deviceClass;
    public string Description => description;
    public string FormFactor => formFactor;
    public string Icon => icon;
    public string IconName => iconName;
    public string IntendedRoles => intendedRoles;
    public string Name => name;
    public string Nick => nick;
    public string Profile => profile;
    public string ProfileSet => profileSet;
    public string Serial => serial;
    public string SubSystem => subSystem;
    public bool AutoPort => autoPort;
    public bool AutoProfile => autoProfile;
    public ulong? ProChannels => proChannels;
    public ulong? ProbeRate => probeRate;
    public bool DisableMixerPath => disableMixerPath;
    public bool IgnoreDb => ignoreDb;
    public string Path => path;
    public bool SoftMixer => softMixer;
    public bool SplitEnable => splitEnable;
    public bool UseUcm => useUcm;
    public string ProductId => productId;
    public string ProductName => productName;
    public string VendorId => vendorId;
    public string VendorName => vendorName;

    public ViewModelActivator Activator { get; } = new();

    public static DeviceDetailsViewModel FromDevice(Device device)
    {
        return new(
            device.ObjectId,
            device.ObjectSerial,
            device.Class ?? string.Empty,
            device.Description ?? string.Empty,
            device.FormFactor ?? string.Empty,
            device.Icon ?? string.Empty,
            device.IconName ?? string.Empty,
            device.IntendedRoles ?? string.Empty,
            device.Name ?? string.Empty,
            device.Nick ?? string.Empty,
            device.Profile ?? string.Empty,
            device.ProfileSet ?? string.Empty,
            device.Serial ?? string.Empty,
            device.SubSystem ?? string.Empty,
            device.Api.Acp.AutoPort,
            device.Api.Acp.AutoProfile,
            device.Api.Acp.ProChannels,
            device.Api.Acp.ProbeRate,
            device.Api.Alsa.DisableMixerPath,
            device.Api.Alsa.IgnoreDb,
            device.Api.Alsa.Path ?? string.Empty,
            device.Api.Alsa.SoftMixer,
            device.Api.Alsa.SplitEnable,
            device.Api.Alsa.UseUcm,
            device.Product.Id ?? string.Empty,
            device.Product.Name ?? string.Empty,
            device.Vendor.Id ?? string.Empty,
            device.Vendor.Name ?? string.Empty);
    }
}