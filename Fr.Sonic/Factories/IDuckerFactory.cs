using Fr.Sonic.Model.Config.Ducker;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Factories;

public interface IDuckerFactory
{
    Task<Ducker> CreateDuckerAsync(DuckerConfig config);
}
