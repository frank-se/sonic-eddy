using Fr.Sonic.Model.Config.MidiManipulator;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Factories;

public interface IMidiManipulatorFactory
{
    Task<MidiManipulator> CreateMidiManipulatorAsync(
        MidiManipulatorConfig config);
}
