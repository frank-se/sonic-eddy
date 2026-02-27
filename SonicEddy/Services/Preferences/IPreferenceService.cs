using System.Threading.Tasks;

namespace SonicEddy.Services.Preferences;

public interface IPreferenceService
{
    Contracts.ApplicationPreferences.Preferences? Preferences { get; }

    Task Load();

    Task UpdateAndSave(
        Contracts.ApplicationPreferences.Preferences preferences);
}