using System.Collections.Generic;
using System.Linq;
using Fr.Sonic.Model;
using Fr.Sonic.Model.Objects;
using ReactiveUI;
using SonicEddy.ViewModels.ModuleManagerViewModels;

namespace SonicEddy.ViewModels.ObjectDetailsViewModels;

public class NodeDetailsViewModel(
    ulong objectId,
    ulong objectSerial,
    bool monitorChannelVolumes,
    bool alwaysProcess,
    bool autoConnect,
    string description,
    bool disabled,
    bool doNotReconnect,
    bool exclusive,
    ulong? forceQuantum,
    ulong? forceRate,
    string linkGroup,
    bool lockQuantum,
    bool lockRate,
    string name,
    bool passive,
    bool pauseOnIdle,
    ulong? rate,
    bool suspendOnIdle,
    bool wantDriver,
    ulong? priorityDriver,
    bool doNotRemixStream,
    string targetObject,
    bool linger,
    string alsaPath,
    bool alsaAutoLink,
    bool alsaDisableBatch,
    bool alsaDisableMemoryMap,
    bool alsaDisableTSched,
    ulong alsaHeadroom,
    bool alsaHTimestamp,
    ulong alsaHTimestampMaxErrors,
    bool alsaMultiRate,
    ulong? alsaPeriodNum,
    ulong? alsaPeriodSize,
    ulong alsaStartDelay,
    bool alsaUseChannelMap,
    string audioAllowedRates,
    ulong audioChannels,
    string audioFormat,
    ulong audioRate,
    bool channelMixDisable,
    ulong channelMixFrontChannelCutOff,
    double channelMixHilbertTaps,
    bool channelMixLockVolumes,
    ulong channelMixLowFrequencyEffectsCutOff,
    double channelMixMaxVolume,
    double channelMixMinVolume,
    bool channelMixMixLowFrequencyEffects,
    bool channelMixNormalize,
    bool channelMixUpMix,
    string channelMixUpMixMethod,
    string clockName,
    ulong? clientId,
    string debugWavePath,
    ulong? deviceId,
    string ditherMethod,
    ulong ditherNoise,
    ulong internalLatencyRate,
    ulong internalLatencyNanoseconds,
    string mediaName,
    string mediaClass,
    string mediaFormat,
    string mediaIcon,
    string mediaIconName,
    string mediaArtist,
    string mediaCategory,
    string mediaComment,
    string mediaCopyright,
    string mediaDate,
    string mediaFilename,
    string mediaLanguage,
    string mediaRole,
    string mediaSoftware,
    string mediaTitle,
    string mediaType,
    bool resampleDisable,
    bool resamplePeaks,
    bool resamplePrefill,
    ulong resampleQuality,
    string loopName,
    string loopClass,
    List<PropertyInfoViewModel> propertyInfos)
    : ObjectDetailsViewModelBase(objectId, objectSerial, "Node"),
        IActivatableViewModel
{
    public bool MonitorChannelVolumes => monitorChannelVolumes;
    public bool AlwaysProcess => alwaysProcess;
    public bool AutoConnect => autoConnect;
    public string Description => description;
    public bool Disabled => disabled;
    public bool DoNotReconnect => doNotReconnect;
    public bool Exclusive => exclusive;
    public ulong? ForceQuantum => forceQuantum;
    public ulong? ForceRate => forceRate;
    public string LinkGroup => linkGroup;
    public bool LockQuantum => lockQuantum;
    public bool LockRate => lockRate;
    public string Name => name;
    public bool Passive => passive;
    public bool PauseOnIdle => pauseOnIdle;
    public ulong? Rate => rate;
    public bool SuspendOnIdle => suspendOnIdle;
    public bool WantDriver => wantDriver;
    public ulong? PriorityDriver => priorityDriver;
    public bool DoNotRemixStream => doNotRemixStream;
    public string TargetObject => targetObject;
    public bool Linger => linger;
    public string AlsaPath => alsaPath;
    public bool AlsaAutoLink => alsaAutoLink;
    public bool AlsaDisableBatch => alsaDisableBatch;
    public bool AlsaDisableMemoryMap => alsaDisableMemoryMap;
    public bool AlsaDisableTSched => alsaDisableTSched;
    public ulong AlsaHeadroom => alsaHeadroom;
    public bool AlsaHTimestamp => alsaHTimestamp;
    public ulong AlsaHTimestampMaxErrors => alsaHTimestampMaxErrors;
    public bool AlsaMultiRate => alsaMultiRate;
    public ulong? AlsaPeriodNum => alsaPeriodNum;
    public ulong? AlsaPeriodSize => alsaPeriodSize;
    public ulong AlsaStartDelay => alsaStartDelay;
    public bool AlsaUseChannelMap => alsaUseChannelMap;
    public string AudioAllowedRates => audioAllowedRates;
    public ulong AudioChannels => audioChannels;
    public string AudioFormat => audioFormat;
    public ulong AudioRate => audioRate;
    public bool ChannelMixDisable => channelMixDisable;
    public ulong ChannelMixFrontChannelCutOff => channelMixFrontChannelCutOff;
    public double ChannelMixHilbertTaps => channelMixHilbertTaps;
    public bool ChannelMixLockVolumes => channelMixLockVolumes;

    public ulong ChannelMixLowFrequencyEffectsCutOff =>
        channelMixLowFrequencyEffectsCutOff;

    public double ChannelMixMaxVolume => channelMixMaxVolume;
    public double ChannelMixMinVolume => channelMixMinVolume;

    public bool ChannelMixMixLowFrequencyEffects =>
        channelMixMixLowFrequencyEffects;

    public bool ChannelMixNormalize => channelMixNormalize;
    public bool ChannelMixUpMix => channelMixUpMix;
    public string ChannelMixUpMixMethod => channelMixUpMixMethod;
    public string ClockName => clockName;
    public ulong? ClientId => clientId;
    public string DebugWavePath => debugWavePath;
    public ulong? DeviceId => deviceId;
    public string DitherMethod => ditherMethod;
    public ulong DitherNoise => ditherNoise;
    public ulong InternalLatencyRate => internalLatencyRate;
    public ulong InternalLatencyNanoseconds => internalLatencyNanoseconds;
    public string MediaName => mediaName;
    public string MediaClass => mediaClass;
    public string MediaFormat => mediaFormat;
    public string MediaIcon => mediaIcon;
    public string MediaIconName => mediaIconName;
    public string MediaArtist => mediaArtist;
    public string MediaCategory => mediaCategory;
    public string MediaComment => mediaComment;
    public string MediaCopyright => mediaCopyright;
    public string MediaDate => mediaDate;
    public string MediaFilename => mediaFilename;
    public string MediaLanguage => mediaLanguage;
    public string MediaRole => mediaRole;
    public string MediaSoftware => mediaSoftware;
    public string MediaTitle => mediaTitle;
    public string MediaType => mediaType;
    public bool ResampleDisable => resampleDisable;
    public bool ResamplePeaks => resamplePeaks;
    public bool ResamplePrefill => resamplePrefill;
    public ulong ResampleQuality => resampleQuality;
    public string LoopName => loopName;
    public string LoopClass => loopClass;

    public List<PropertyInfoViewModel> PropertyInfos => propertyInfos;

    public ViewModelActivator Activator { get; } = new();

    public static NodeDetailsViewModel FromNode(Node device)
    {
        var propertyInfos = device.PropertyInfos.IsCompleted
            ? device.PropertyInfos.Result.PropertyInfos
                .Select(PropertyInfoViewModel.FromPropertyInfo).ToList()
            : [];
        return new(
            device.ObjectId,
            device.ObjectSerial,
            device.MonitorChannelVolumes,
            device.AlwaysProcess,
            device.AutoConnect,
            device.Description ?? string.Empty,
            device.Disabled,
            device.DoNotReconnect,
            device.Exclusive,
            device.ForceQuantum,
            device.ForceRate,
            device.LinkGroup ?? string.Empty,
            device.LockQuantum,
            device.LockRate,
            device.Name ?? string.Empty,
            device.Passive,
            device.PauseOnIdle,
            device.Rate,
            device.SuspendOnIdle,
            device.WantDriver,
            device.PriorityDriver,
            device.DoNotRemixStream,
            device.TargetObject ?? string.Empty,
            device.Object.Linger,
            device.Alsa.Path ?? string.Empty,
            device.Alsa.AutoLink,
            device.Alsa.DisableBatch,
            device.Alsa.DisableMemoryMap,
            device.Alsa.DisableTSched,
            device.Alsa.Headroom,
            device.Alsa.HTimeStamp,
            device.Alsa.HTimeStampMaxErrors,
            device.Alsa.MultiRate,
            device.Alsa.PeriodNum,
            device.Alsa.PeriodSize,
            device.Alsa.StartDelay,
            device.Alsa.UseChannelMap,
            device.Audio.AllowedRates ?? string.Empty,
            device.Audio.Channels,
            device.Audio.Format ?? string.Empty,
            device.Audio.Rate,
            device.ChannelMix.Disable,
            device.ChannelMix.FrontChannelCutOff,
            device.ChannelMix.HilbertTaps,
            device.ChannelMix.LockVolumes,
            device.ChannelMix.LowFrequencyEffectsCutOff,
            device.ChannelMix.MaxVolume,
            device.ChannelMix.MinVolume,
            device.ChannelMix.MixLowFrequencyEffects,
            device.ChannelMix.Normalize,
            device.ChannelMix.UpMix,
            device.ChannelMix.UpMixMethod ?? string.Empty,
            device.Clock.Name ?? string.Empty,
            device.Client?.Id,
            device.Debug.Path ?? string.Empty,
            device.DeviceAssignment?.Id,
            device.Dither.Method ?? string.Empty,
            device.Dither.Noise,
            device.InternalLatency.Rate,
            device.InternalLatency.Nanoseconds,
            device.Media.Name ?? string.Empty,
            device.Media.Class ?? string.Empty,
            device.Media.Format ?? string.Empty,
            device.Media.Icon ?? string.Empty,
            device.Media.IconName ?? string.Empty,
            device.Media.Artist ?? string.Empty,
            device.Media.Category ?? string.Empty,
            device.Media.Comment ?? string.Empty,
            device.Media.Copyright ?? string.Empty,
            device.Media.Date ?? string.Empty,
            device.Media.FileName ?? string.Empty,
            device.Media.Language ?? string.Empty,
            device.Media.Role ?? string.Empty,
            device.Media.Software ?? string.Empty,
            device.Media.Title ?? string.Empty,
            device.Media.Type ?? string.Empty,
            device.Resample.Disable,
            device.Resample.Peaks,
            device.Resample.Prefill,
            device.Resample.Quality,
            device.Loop.Name ?? string.Empty,
            device.Loop.Class ?? string.Empty,
            propertyInfos
        );
    }
}