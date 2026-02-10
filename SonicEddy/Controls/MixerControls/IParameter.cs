namespace SonicEddy.Controls.MixerControls;

public interface IParameter
{
    public float Value { get; set; }
    public float Minimum { get; }
    public float Maximum { get; }
    public string Name { get; }
    public bool IsMainParameter { get; }
}