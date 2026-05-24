using Fr.Sonic.Model.Config.Looper;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Factories;

public interface ILooperFactory
{
    Task<Looper> CreateLooperAsync(LooperConfig config);
}
