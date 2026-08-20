namespace SonicEddy.Services.VideoStreaming;

public interface IMixerOverviewStreamService
{
    bool IsRunning { get; }

    void Start();

    void Stop();
}
