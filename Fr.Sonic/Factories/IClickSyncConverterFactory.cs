using Fr.Sonic.Model.Config.ClickSync;
using Fr.Sonic.Modules.Models;

namespace Fr.Sonic.Factories;

public interface IClickSyncConverterFactory
{
    Task<ClickSyncConverter> CreateClickSyncConverterAsync(
        ClickSyncConverterConfig config);
}
