namespace SonicEddy.Controls.MixerControls;

public interface IRoutingTarget
{
    public string Name { get; }
    public IChannel Channel { get; }
}