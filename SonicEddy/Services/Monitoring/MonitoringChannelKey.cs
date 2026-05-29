namespace SonicEddy.Services.Monitoring;

public enum MonitoringChannelType { Strip, Group, Master, Return }

public record MonitoringChannelKey(
    int LayerIndex,
    MonitoringChannelType ChannelType,
    int ChannelIndex);
